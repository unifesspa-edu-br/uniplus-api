namespace Unifesspa.UniPlus.Selecao.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Application.DTOs;
using Application.Queries.ProcessosSeletivos;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Kernel.Results;

[ApiController]
[Route("processos-seletivos/{id:guid}/indicadores")]
[Authorize]
[SuppressMessage("Performance", "CA1515:Consider making public types internal", Justification = "ASP.NET Core só descobre controllers públicos.")]
public sealed class IndicadoresDoProcessoController : ControllerBase
{
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;

    public IndicadoresDoProcessoController(IQueryBus queryBus, IDomainErrorMapper mapper)
    {
        _queryBus = queryBus;
        _mapper = mapper;
    }

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [VendorMediaType(Resource = "indicadores-do-processo", Versions = [1])]
    [ProducesResponseType(typeof(IndicadoresDoProcessoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> Obter(Guid id, CancellationToken cancellationToken)
    {
        Result<IndicadoresDoProcessoDto> resultado = await _queryBus
            .Send(new ObterIndicadoresDoProcessoQuery(id), cancellationToken).ConfigureAwait(false);
        return resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
    }
}
