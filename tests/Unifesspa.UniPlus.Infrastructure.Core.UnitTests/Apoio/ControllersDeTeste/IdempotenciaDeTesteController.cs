namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Apoio.ControllersDeTeste;

using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;

[ApiController]
[Route("teste/idempotencia/[action]")]
public sealed class IdempotenciaDeTesteController : ControllerBase
{
    [HttpPost, RequiresIdempotencyKey]
    public IActionResult SemTeto() => NoContent();
    [HttpPost, RequiresIdempotencyKey(MaxBodyBytes = 200)]
    public IActionResult ComTetoMaiorQueOGlobal() => NoContent();
    [HttpPost, RequiresIdempotencyKey(MaxBodyBytes = 10)]
    public IActionResult ComTetoMenorQueOGlobal() => NoContent();
    [HttpPost, RequiresIdempotencyKey]
    public IActionResult IdempotentAction() => NoContent();
    [HttpPost]
    public IActionResult NonIdempotentAction() => NoContent();
}
