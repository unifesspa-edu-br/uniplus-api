namespace Unifesspa.UniPlus.Infrastructure.Core.Idempotency;

/// <summary>
/// Estado de uma entrada do cache de idempotência. Usado para distinguir
/// reservation (pré-handler) de completed (pós-handler com response cifrada).
/// </summary>
public enum IdempotencyStatus
{
    /// <summary>Reservation criada mas handler ainda em execução.</summary>
    Processing = 0,

    /// <summary>Handler completou; response disponível para replay.</summary>
    Completed = 1,
}
