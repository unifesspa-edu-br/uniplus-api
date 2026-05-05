namespace Unifesspa.UniPlus.Infrastructure.Core.Idempotency;

/// <summary>Resultado discriminante de <see cref="IIdempotencyStore.LookupAsync"/>.</summary>
public enum IdempotencyOutcome
{
    /// <summary>Sem entrada no cache para a chave de lookup. Handler deve rodar.</summary>
    Miss = 0,

    /// <summary>Entrada existe + body_hash bate. Replay verbatim da response cacheada.</summary>
    HitMatch = 1,

    /// <summary>Entrada existe + body_hash diverge. 422 body_mismatch.</summary>
    HitMismatch = 2,

    /// <summary>Entrada existe mas ainda em status Processing. 409 processing_conflict.</summary>
    Processing = 3,
}

public sealed record IdempotencyLookupResult(IdempotencyOutcome Outcome, IdempotencyEntry? Entry);
