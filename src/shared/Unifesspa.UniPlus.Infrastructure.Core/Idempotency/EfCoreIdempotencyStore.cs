namespace Unifesspa.UniPlus.Infrastructure.Core.Idempotency;

using Microsoft.EntityFrameworkCore;

using Npgsql;

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

        if (entry is null)
            return new IdempotencyLookupResult(IdempotencyOutcome.Miss, null);

        // Entry expirada conta como miss, mas precisa ser DELETADA antes de
        // retornar — caso contrário a UNIQUE em (scope, endpoint, key) bloqueia
        // o próximo TryReserve do cliente (UNIQUE violation), resultando em 409
        // espúrio para uma request que deveria ser tratada como nova.
        // ExecuteDelete com filtro Id+ExpiresAt é seguro contra concorrência:
        // outra request que detecte o mesmo expirado vence o DELETE, a perdedora
        // vê 0 rows e segue normal.
        DateTimeOffset now = _timeProvider.GetUtcNow();
        if (entry.ExpiresAt <= now)
        {
            await _db.Set<IdempotencyEntry>()
                .Where(e => e.Id == entry.Id && e.ExpiresAt <= now)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);
            return new IdempotencyLookupResult(IdempotencyOutcome.Miss, null);
        }

        if (entry.Status == IdempotencyStatus.Processing)
            return new IdempotencyLookupResult(IdempotencyOutcome.Processing, entry);

        if (!string.Equals(entry.BodyHash, bodyHash, StringComparison.Ordinal))
            return new IdempotencyLookupResult(IdempotencyOutcome.HitMismatch, entry);

        return new IdempotencyLookupResult(IdempotencyOutcome.HitMatch, entry);
    }

    public async Task<bool> TryReserveAsync(
        string scope, string endpoint, string idempotencyKey, string bodyHash,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        IdempotencyEntry entry = new()
        {
            Scope = scope,
            Endpoint = endpoint,
            IdempotencyKey = idempotencyKey,
            BodyHash = bodyHash,
            Status = IdempotencyStatus.Processing,
            ExpiresAt = expiresAt,
            CreatedAt = _timeProvider.GetUtcNow(),
        };

        _db.Set<IdempotencyEntry>().Add(entry);

        try
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Apenas violação UNIQUE (race com outra request) sinaliza
            // "reserva concorrente" — outros DbUpdateException (FK, disco
            // cheio, lock timeout) propagam para a infra de logs e cliente
            // recebe 5xx, ao invés de serem mascarados como 409 ProcessingConflict.
            _db.Set<IdempotencyEntry>().Entry(entry).State = EntityState.Detached;
            return false;
        }
    }

    public async Task CompleteAsync(
        string scope, string endpoint, string idempotencyKey,
        int responseStatus, string? responseHeadersJson, byte[] responseBodyCipher,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(responseBodyCipher);

        // ExecuteUpdate emite UPDATE atômico sem carregar/rastrear a entry —
        // evita commitar estado fantasma do request scope (ChangeTracker poderia
        // ter outras entities pendentes do controller). Filtro por
        // Status == Processing é idempotente: chamada dupla não sobrescreve
        // entry já Completed (segunda chamada simplesmente atualiza 0 rows).
        int updated = await _db.Set<IdempotencyEntry>()
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

        if (updated == 0)
        {
            // Não é erro: entry pode já ter sido Completed por concorrente
            // (raro, exigiria duas reservations idênticas mas a UNIQUE
            // impede) ou TTL-expirada antes do Complete. Log noop.
        }
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

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException pg && pg.SqlState == PostgresUniqueViolationSqlState;
}
