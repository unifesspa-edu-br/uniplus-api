namespace Unifesspa.UniPlus.Infrastructure.Core.Idempotency;

/// <summary>
/// O que identifica uma requisição idempotente no cache: quem pediu, em que endpoint, com qual chave, e o hash do corpo
/// que a chave está presa a.
/// </summary>
internal sealed record IdempotencyRequestKey(string Scope, string Endpoint, string IdempotencyKey, string BodyHash);
