namespace Unifesspa.UniPlus.Infrastructure.Core.Idempotency;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Http;

using Errors;

/// <summary>
/// Mapeia codes de domínio do filter de idempotência para HTTP status +
/// type URL (ADR-0027 §"Lookup, replay e validação"). Auto-registrado via
/// <c>AddIdempotency&lt;TDbContext&gt;</c>.
/// </summary>
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via IServiceProvider em AddIdempotency().")]
internal sealed class IdempotencyDomainErrorRegistration : IDomainErrorRegistration
{
    public IEnumerable<KeyValuePair<string, DomainErrorMapping>> GetMappings() =>
    [
        new(IdempotencyDomainErrorCodes.KeyAusente,
            new DomainErrorMapping(StatusCodes.Status400BadRequest, "uniplus.idempotency.key_ausente",
                "Header Idempotency-Key obrigatório neste endpoint")),
        new(IdempotencyDomainErrorCodes.KeyMalformada,
            new DomainErrorMapping(StatusCodes.Status400BadRequest, "uniplus.idempotency.key_malformada",
                "Header Idempotency-Key malformado")),
        new(IdempotencyDomainErrorCodes.BodyMismatch,
            new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.idempotency.body_mismatch",
                "Mesma Idempotency-Key reusada com body diferente")),
        new(IdempotencyDomainErrorCodes.ProcessingConflict,
            new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.idempotency.processing_conflict",
                "Request com a mesma Idempotency-Key ainda em processamento; tentar novamente em alguns segundos")),
        new(IdempotencyDomainErrorCodes.PrincipalRequerido,
            new DomainErrorMapping(StatusCodes.Status401Unauthorized, "uniplus.idempotency.principal_requerido",
                "Endpoint com Idempotency-Key requer principal autenticado")),
        new(IdempotencyDomainErrorCodes.BodyMuitoGrande,
            new DomainErrorMapping(StatusCodes.Status413PayloadTooLarge, "uniplus.idempotency.body_muito_grande",
                "Request body excede o limite aceito por endpoints idempotentes")),
    ];
}
