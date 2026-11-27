namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Apoio.ControllersDeTeste;

using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Pagination;

[ApiController]
[Route("teste/cursor/[action]")]
public sealed class CursorDeTesteController : ControllerBase
{
    [HttpGet]
    public IActionResult PublicEditais([FromCursor("editais")] PageRequest request) => Ok(request);
    [HttpGet]
    public IActionResult UserInscricoes([FromCursor("inscricoes", RequireUserBinding = true)] PageRequest request) => Ok(request);
    [HttpGet]
    public IActionResult ScopedByEntity([FromCursor("atos", "entidadeTipo", "entidadeId")] PageRequest request) => Ok(request);
    [HttpGet]
    public IActionResult WithoutAttribute([FromQuery] PageRequest request) => Ok(request);
    [HttpGet]
    public IActionResult Listar([FromCursor("editais")] PageRequest page) => Ok(page);
    [HttpGet]
    public IActionResult ListarComFiltros(
        [FromCursor("editais")] PageRequest page,
        [FromQuery(Name = "q")] string? busca,
        [FromQuery(Name = "busca")] string? buscaPorExtenso,
        [FromQuery(Name = "tipo")] int[]? tipos,
        [FromQuery(Name = "tipoProcesso")] string? tipoProcesso) => Ok(new { page, busca, buscaPorExtenso, tipos, tipoProcesso });
    [HttpGet]
    public IActionResult ListarComFiltroComposto([FromCursor("editais")] PageRequest page, [FromQuery] FiltroDeTeste filtro) => Ok(new { page, filtro });
    [HttpGet]
    public IActionResult SemCursor(int id) => Ok(id);
}

public sealed class FiltroDeTeste
{
    public string? Cadastro { get; init; }

    public string[]? Ato { get; init; }
}

[ApiController]
[Route("teste/fora/[action]")]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class ForaDoContratoDeTesteController : ControllerBase
{
    [HttpGet]
    public OkObjectResult Listar([FromCursor("editais")] PageRequest page) => Ok(page);
}
