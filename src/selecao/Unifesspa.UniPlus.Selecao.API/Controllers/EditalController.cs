namespace Unifesspa.UniPlus.Selecao.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.Editais;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.Editais;

[ApiController]
[Route("api/editais")]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public; sem isso o MVC ignora a classe e nenhum endpoint é registrado.")]
public sealed class EditalController : ControllerBase
{
    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;

    public EditalController(ICommandBus commandBus, IQueryBus queryBus, IDomainErrorMapper mapper)
    {
        _commandBus = commandBus;
        _queryBus = queryBus;
        _mapper = mapper;
    }

    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Criar([FromBody] CriarEditalCommand command, CancellationToken cancellationToken)
    {
        Result<Guid> resultado = await _commandBus.Send(command, cancellationToken);
        if (resultado.IsSuccess)
            return CreatedAtAction(nameof(ObterPorId), new { id = resultado.Value }, resultado.Value);
        return resultado.ToActionResult(_mapper);
    }

    [HttpGet("{id:guid}")]
    [VendorMediaType(Resource = "edital", Versions = [1])]
    [ProducesResponseType(typeof(EditalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken cancellationToken)
    {
        EditalDto? edital = await _queryBus.Send(new ObterEditalQuery(id), cancellationToken);
        return edital is not null ? Ok(edital) : NotFound();
    }

    // Despacha PublicarEditalCommand pelo ICommandBus (Wolverine). O handler
    // convention-based atualiza o agregado e drena EditalPublicadoEvent por
    // cascading messages — atomicidade write+evento garantida pela
    // IEnvelopeTransaction da configuração produtiva (ADR-0005).
    [HttpPost("{id:guid}/publicar")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Publicar(Guid id, CancellationToken cancellationToken)
    {
        Result resultado = await _commandBus.Send(new PublicarEditalCommand(id), cancellationToken);
        if (resultado.IsSuccess)
            return NoContent();
        return resultado.ToActionResult(_mapper);
    }
}
