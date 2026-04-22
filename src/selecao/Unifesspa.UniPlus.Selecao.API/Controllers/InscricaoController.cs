namespace Unifesspa.UniPlus.Selecao.API.Controllers;

using MediatR;

using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Selecao.Application.Commands.Inscricoes;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.Inscricoes;
using Unifesspa.UniPlus.Kernel.Results;

[ApiController]
[Route("api/v1/inscricoes")]
internal sealed class InscricaoController : ControllerBase
{
    private readonly ISender _sender;

    public InscricaoController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Criar([FromBody] CriarInscricaoCommand command, CancellationToken cancellationToken)
    {
        Result<Guid> resultado = await _sender.Send(command, cancellationToken);
        return resultado.IsSuccess
            ? CreatedAtAction(nameof(ObterPorId), new { id = resultado.Value }, resultado.Value)
            : BadRequest(resultado.Error);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(InscricaoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken cancellationToken)
    {
        Result<InscricaoDto> resultado = await _sender.Send(new ObterInscricaoQuery(id), cancellationToken);
        return resultado.IsSuccess
            ? Ok(resultado.Value)
            : NotFound(resultado.Error);
    }
}
