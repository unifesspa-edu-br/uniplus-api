namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Apoio.ControllersDeTeste;

using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Infrastructure.Core.OpenApi;

[ApiController, Route("teste/cors/trava")]
public sealed class ControllerSujeitoATrava : ControllerBase
{
    [HttpPost, EmiteEsperaEsgotadaPelaTrava]
    public IActionResult Alterar() => NoContent();
}

[ApiController, Route("teste/cors/location")]
public sealed class ControllerQueDeclaraLocation : ControllerBase
{
    [HttpPost, EmiteHeader("Location", "Endereço do recurso criado.", Status = "201"), EmiteETag(Descricao = "Selo da representação.")]
    public IActionResult Criar() => NoContent();
}

[ApiController, Route("teste/cors/outro")]
public sealed class OutroControllerQueDeclaraLocation : ControllerBase
{
    [HttpPost, EmiteHeader("location", "Endereço do recurso criado.", Status = "201")]
    public IActionResult Criar() => NoContent();
}

[ApiController, Route("teste/cors/sem-header")]
public sealed class ControllerQueNaoDeclaraNada : ControllerBase
{
    [HttpGet]
    public IActionResult Listar() => NoContent();
}
