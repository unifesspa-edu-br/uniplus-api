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
    public IActionResult SemCursor(int id) => Ok(id);
}
