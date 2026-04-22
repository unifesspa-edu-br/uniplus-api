namespace Unifesspa.UniPlus.Ingresso.API.Controllers;

using MediatR;

using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Ingresso.Application.Commands.Matriculas;
using Unifesspa.UniPlus.Ingresso.Application.DTOs;
using Unifesspa.UniPlus.Ingresso.Application.Queries.Matriculas;
using Unifesspa.UniPlus.Kernel.Results;

[ApiController]
[Route("api/v1/matriculas")]
internal sealed class MatriculaController : ControllerBase
{
    private readonly ISender _sender;

    public MatriculaController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Criar([FromBody] IniciarMatriculaCommand command, CancellationToken cancellationToken)
    {
        Result<Guid> resultado = await _sender.Send(command, cancellationToken);
        return resultado.IsSuccess
            ? CreatedAtAction(nameof(ObterPorId), new { id = resultado.Value }, resultado.Value)
            : BadRequest(resultado.Error);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(MatriculaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken cancellationToken)
    {
        Result<MatriculaDto> resultado = await _sender.Send(new ObterMatriculaQuery(id), cancellationToken);
        return resultado.IsSuccess
            ? Ok(resultado.Value)
            : NotFound(resultado.Error);
    }
}
