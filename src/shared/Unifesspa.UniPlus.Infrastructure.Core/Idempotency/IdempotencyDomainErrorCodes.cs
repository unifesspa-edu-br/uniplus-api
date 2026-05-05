namespace Unifesspa.UniPlus.Infrastructure.Core.Idempotency;

/// <summary>
/// Códigos de domain error emitidos pelo <c>IdempotencyFilter</c>. Resolvidos
/// para HTTP status pelo <see cref="IdempotencyDomainErrorRegistration"/>
/// (ADR-0024).
/// </summary>
public static class IdempotencyDomainErrorCodes
{
    public const string KeyAusente = "Idempotency.KeyAusente";
    public const string KeyMalformada = "Idempotency.KeyMalformada";
    public const string BodyMismatch = "Idempotency.BodyMismatch";
    public const string ProcessingConflict = "Idempotency.ProcessingConflict";
    public const string PrincipalRequerido = "Idempotency.PrincipalRequerido";
    public const string BodyMuitoGrande = "Idempotency.BodyMuitoGrande";
}
