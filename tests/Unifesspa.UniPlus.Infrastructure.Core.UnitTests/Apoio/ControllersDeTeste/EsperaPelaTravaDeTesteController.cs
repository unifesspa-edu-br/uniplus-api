namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Apoio.ControllersDeTeste;

using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Infrastructure.Core.OpenApi;

[ApiController]
[Route("teste/trava/[action]")]
public sealed class RotaDeTravaDeTesteController : ControllerBase
{
    [HttpPost, EmiteEsperaEsgotadaPelaTrava, RequiresIdempotencyKey]
    public IActionResult ComChave() => NoContent();
    [HttpPost, EmiteEsperaEsgotadaPelaTrava]
    public IActionResult SemChave() => NoContent();
    [HttpPost, RequiresIdempotencyKey]
    public IActionResult SemDeclaracaoComChave() => NoContent();
    [HttpPost]
    public IActionResult SemDeclaracao() => NoContent();
}

[ApiController, Route("teste/trava-marcada/[action]"), EmiteEsperaEsgotadaPelaTrava]
public sealed class TravaMarcadaDeTesteController : ControllerBase
{
    [HttpPut, HttpPost, RequiresIdempotencyKey]
    public IActionResult Mutar() => NoContent();
    [HttpPost]
    public IActionResult MutarSemChave() => NoContent();
    [HttpGet, RequiresIdempotencyKey]
    public IActionResult Ler() => NoContent();
    [HttpHead, RequiresIdempotencyKey]
    public IActionResult LerHead() => NoContent();
}

[ApiController, Route("teste/trava-sem-marca/[action]")]
public sealed class TravaSemMarcaDeTesteController : ControllerBase
{
    [HttpPut]
    public IActionResult Mutar() => NoContent();
    [HttpPut, EmiteEsperaEsgotadaPelaTrava]
    public IActionResult MutarMarcada() => NoContent();
}
