namespace Unifesspa.UniPlus.Portal.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Endpoint dummy do esqueleto Portal — único propósito é popular o documento
/// OpenAPI 3.1 com pelo menos uma operação documentada, validando o pipeline
/// de transformers (info/operation/schema). Anônimo por desenho. Será
/// substituído por endpoints de domínio quando o Portal sair do esqueleto
/// (issue #336).
/// </summary>
[ApiController]
[Route("api/portal/ping")]
[AllowAnonymous]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public; sem isso o MVC ignora a classe e nenhum endpoint é registrado.")]
public sealed class PingController : ControllerBase
{
    private readonly TimeProvider _timeProvider;

    public PingController(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PingResponse), StatusCodes.Status200OK)]
    public IActionResult Pong()
    {
        return Ok(new PingResponse(true, _timeProvider.GetUtcNow()));
    }
}
