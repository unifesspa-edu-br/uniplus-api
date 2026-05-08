namespace Unifesspa.UniPlus.Portal.API.Controllers;

/// <summary>
/// Resposta do endpoint de smoke test <c>GET /api/portal/ping</c>. Existe
/// somente para popular o documento OpenAPI 3.1 com pelo menos um schema
/// documentado, validando o pipeline de transformers (info/operation/schema).
/// Será removido quando o domínio do Portal nascer.
/// </summary>
internal sealed record PingResponse(bool Pong, DateTimeOffset Timestamp);
