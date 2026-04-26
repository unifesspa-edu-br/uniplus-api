namespace Unifesspa.UniPlus.Selecao.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.Editais;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.Editais;

[ApiController]
[Route("api/v1/editais")]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public; sem isso o MVC ignora a classe e nenhum endpoint é registrado.")]
public sealed class EditalController : ControllerBase
{
    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;

    public EditalController(ICommandBus commandBus, IQueryBus queryBus)
    {
        _commandBus = commandBus;
        _queryBus = queryBus;
    }

    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Criar([FromBody] CriarEditalCommand command, CancellationToken cancellationToken)
    {
        Result<Guid> resultado = await _commandBus.Send(command, cancellationToken);
        return resultado.IsSuccess
            ? CreatedAtAction(nameof(ObterPorId), new { id = resultado.Value }, resultado.Value)
            : BadRequest(resultado.Error);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(EditalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken cancellationToken)
    {
        EditalDto? edital = await _queryBus.Send(new ObterEditalQuery(id), cancellationToken);
        return edital is not null ? Ok(edital) : NotFound();
    }

    // Despacha PublicarEditalCommand pelo ICommandBus (Wolverine). O handler
    // convention-based atualiza o agregado e drena EditalPublicadoEvent por
    // cascading messages — atomicidade write+evento garantida pela
    // IEnvelopeTransaction da configuração produtiva (ADR-026).
    [HttpPost("{id:guid}/publicar")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Publicar(Guid id, CancellationToken cancellationToken)
    {
        Result resultado = await _commandBus.Send(new PublicarEditalCommand(id), cancellationToken);
        if (resultado.IsSuccess)
        {
            return NoContent();
        }

        return resultado.Error!.Code == "Edital.NaoEncontrado"
            ? NotFound(resultado.Error)
            : BadRequest(resultado.Error);
    }
}
