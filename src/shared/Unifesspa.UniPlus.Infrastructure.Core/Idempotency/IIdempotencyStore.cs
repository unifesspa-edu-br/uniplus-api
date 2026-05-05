namespace Unifesspa.UniPlus.Infrastructure.Core.Idempotency;

/// <summary>
/// Persistência do cache de Idempotency-Key (ADR-0027). Usado pelo
/// <c>IdempotencyFilter</c> em duas fases: <c>TryReserveAsync</c> antes do
/// handler (cria entry com status Processing — UNIQUE index segura
/// concorrência) e <c>CompleteAsync</c> após o handler (preenche response
/// cifrada + status Completed).
/// </summary>
public interface IIdempotencyStore
{
    /// <summary>
    /// Lookup atômico que decide o branch do filter (Miss / HitMatch /
    /// HitMismatch / Processing). Não muta estado.
    /// </summary>
    Task<IdempotencyLookupResult> LookupAsync(
        string scope, string endpoint, string idempotencyKey, string bodyHash,
        CancellationToken cancellationToken);

    /// <summary>
    /// Tenta criar a reservation. Retorna <c>true</c> se inseriu;
    /// <c>false</c> se UNIQUE violation (race com outra request que
    /// inseriu primeiro). Em caso de <c>false</c>, caller deve
    /// re-fazer lookup para descobrir o estado.
    /// </summary>
    Task<bool> TryReserveAsync(
        string scope, string endpoint, string idempotencyKey, string bodyHash,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken);

    /// <summary>
    /// Atualiza reservation com response cifrada e marca Completed. Idempotente:
    /// se entry não existe ou já está Completed, a operação é no-op (UPDATE
    /// matcha 0 rows).
    /// </summary>
    Task CompleteAsync(
        string scope, string endpoint, string idempotencyKey,
        int responseStatus, string? responseHeadersJson, byte[] responseBodyCipher,
        CancellationToken cancellationToken);

    /// <summary>
    /// Remove reservation. Chamado em 5xx ou Canceled para liberar a key e
    /// permitir retry do cliente após falha do handler (ADR-0027 §"Status
    /// codes em cache" — 5xx nunca cacheia).
    /// </summary>
    Task DeleteAsync(
        string scope, string endpoint, string idempotencyKey,
        CancellationToken cancellationToken);
}
