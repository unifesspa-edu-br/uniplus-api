namespace Unifesspa.UniPlus.Infrastructure.Core.Idempotency;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Unifesspa.UniPlus.Kernel.Extensions;

/// <summary>
/// Implementação <see cref="IIdempotencyStore"/> sobre <see cref="DbContext"/>
/// genérico. Cada módulo que usa idempotência registra um instance via
/// <c>AddIdempotency&lt;TDbContext&gt;</c> (ex.: <c>SelecaoDbContext</c>);
/// a entidade <see cref="IdempotencyEntry"/> precisa estar mapeada no
/// contexto do módulo (auto via <c>ApplyConfigurationsFromAssembly</c>).
/// </summary>
/// <remarks>
/// Trade-off documentado em ADR-0027 §"Negativas — Atomicidade parcial":
/// TryReserve, Complete e Delete rodam em transações curtas separadas — não
/// na mesma <c>IEnvelopeTransaction</c> do agregado. UNIQUE index garante que
/// duas reservations concorrentes não criam duplicação. Em 5xx/Canceled o
/// filter chama <see cref="DeleteAsync"/> para liberar a key (cliente pode
/// retry após falha transitória do servidor).
/// </remarks>
public sealed class EfCoreIdempotencyStore<TDbContext> : IIdempotencyStore
    where TDbContext : DbContext
{
    private const string PostgresUniqueViolationSqlState = "23505";

    private readonly TDbContext _db;
    private readonly TimeProvider _timeProvider;

    public EfCoreIdempotencyStore(TDbContext db, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _db = db;
        _timeProvider = timeProvider;
    }

    public async Task<IdempotencyLookupResult> LookupAsync(
        string scope, string endpoint, string idempotencyKey, string bodyHash,
        CancellationToken cancellationToken)
    {
        IdempotencyEntry? entry = await _db.Set<IdempotencyEntry>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                e => e.Scope == scope && e.Endpoint == endpoint && e.IdempotencyKey == idempotencyKey,
                cancellationToken)
            .ConfigureAwait(false);

        if (entry is null || await DescartarSeExpiradaAsync(entry, cancellationToken).ConfigureAwait(false))
            return new IdempotencyLookupResult(IdempotencyOutcome.Miss, null);

        return Classificar(entry, bodyHash);
    }

    /// <summary>
    /// Reserva a chave numa transação curta própria. Só a violação UNIQUE (corrida com outra request) sinaliza
    /// reserva concorrente; outros <see cref="DbUpdateException"/> (FK, disco cheio, lock timeout) propagam para a
    /// infra de logs e o cliente recebe 5xx, em vez de serem mascarados como 409 ProcessingConflict.
    /// </summary>
    public async Task<bool> TryReserveAsync(
        string scope, string endpoint, string idempotencyKey, string bodyHash,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        IdempotencyEntry entry = NovaReserva(scope, endpoint, idempotencyKey, bodyHash, expiresAt);
        _db.Set<IdempotencyEntry>().Add(entry);

        try
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            _db.Set<IdempotencyEntry>().Entry(entry).State = EntityState.Detached;
            return false;
        }
    }

    /// <summary>
    /// Conclui a reserva com um UPDATE atômico, sem carregar nem rastrear a entry: evita commitar estado fantasma do
    /// request scope (o ChangeTracker pode ter outras entities pendentes do controller). O filtro por
    /// <c>Status == Processing</c> é idempotente: chamada dupla não sobrescreve entry já concluída, e o UPDATE que
    /// não alcança nenhuma linha (concluída por concorrente ou expirada antes) não é erro.
    /// </summary>
    public async Task CompleteAsync(
        string scope, string endpoint, string idempotencyKey,
        int responseStatus, string? responseHeadersJson, byte[] responseBodyCipher,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(responseBodyCipher);

        await _db.Set<IdempotencyEntry>()
            .Where(e => e.Scope == scope
                && e.Endpoint == endpoint
                && e.IdempotencyKey == idempotencyKey
                && e.Status == IdempotencyStatus.Processing)
            .ExecuteUpdateAsync(s => s
                .SetProperty(e => e.Status, IdempotencyStatus.Completed)
                .SetProperty(e => e.ResponseStatus, (int?)responseStatus)
                .SetProperty(e => e.ResponseHeadersJson, responseHeadersJson)
                .SetProperty(e => e.ResponseBodyCipher, responseBodyCipher),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task DeleteAsync(
        string scope, string endpoint, string idempotencyKey,
        CancellationToken cancellationToken)
    {
        await _db.Set<IdempotencyEntry>()
            .Where(e => e.Scope == scope
                && e.Endpoint == endpoint
                && e.IdempotencyKey == idempotencyKey)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Entry expirada conta como miss, mas precisa ser DELETADA antes de retornar: sem isso a UNIQUE em
    /// (scope, endpoint, key) bloquearia o próximo TryReserve do cliente, dando 409 espúrio a uma request que deveria
    /// ser tratada como nova. O DELETE filtrado por Id e ExpiresAt é seguro contra concorrência: a request que perde
    /// a corrida vê 0 linhas e segue normal.
    /// </summary>
    private async Task<bool> DescartarSeExpiradaAsync(IdempotencyEntry entry, CancellationToken cancellationToken)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        if (entry.ExpiresAt > now)
            return false;

        await _db.Set<IdempotencyEntry>()
            .Where(e => e.Id == entry.Id && e.ExpiresAt <= now)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private static IdempotencyLookupResult Classificar(IdempotencyEntry entry, string bodyHash)
    {
        if (entry.Status == IdempotencyStatus.Processing)
            return new IdempotencyLookupResult(IdempotencyOutcome.Processing, entry);

        return string.Equals(entry.BodyHash, bodyHash, StringComparison.Ordinal)
            ? new IdempotencyLookupResult(IdempotencyOutcome.HitMatch, entry)
            : new IdempotencyLookupResult(IdempotencyOutcome.HitMismatch, entry);
    }

    private IdempotencyEntry NovaReserva(
        string scope, string endpoint, string idempotencyKey, string bodyHash, DateTimeOffset expiresAt) =>
        new()
        {
            Scope = scope,
            Endpoint = endpoint,
            IdempotencyKey = idempotencyKey,
            BodyHash = bodyHash,
            Status = IdempotencyStatus.Processing,
            ExpiresAt = expiresAt.TruncadoEmMicrossegundos(),
            CreatedAt = _timeProvider.GetUtcNow(),
        };

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException pg && pg.SqlState == PostgresUniqueViolationSqlState;
}
