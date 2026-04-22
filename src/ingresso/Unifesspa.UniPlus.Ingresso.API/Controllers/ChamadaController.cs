namespace Unifesspa.UniPlus.Ingresso.API.Controllers;

using MediatR;

using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Ingresso.Application.Commands.Chamadas;
using Unifesspa.UniPlus.Ingresso.Application.DTOs;
using Unifesspa.UniPlus.Ingresso.Application.Queries.Chamadas;
using Unifesspa.UniPlus.Kernel.Results;

[ApiController]
[Route("api/v1/chamadas")]
internal sealed class ChamadaController : ControllerBase
{
    private readonly ISender _sender;

    public ChamadaController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Criar([FromBody] CriarChamadaCommand command, CancellationToken cancellationToken)
    {
        Result<Guid> resultado = await _sender.Send(command, cancellationToken);
        return resultado.IsSuccess
            ? CreatedAtAction(nameof(ObterPorId), new { id = resultado.Value }, resultado.Value)
            : BadRequest(resultado.Error);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ChamadaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken cancellationToken)
    {
        Result<ChamadaDto> resultado = await _sender.Send(new ObterChamadaQuery(id), cancellationToken);
        return resultado.IsSuccess
            ? Ok(resultado.Value)
            : NotFound(resultado.Error);
    }
}
