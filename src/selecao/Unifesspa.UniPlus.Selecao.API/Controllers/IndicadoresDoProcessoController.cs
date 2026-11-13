namespace Unifesspa.UniPlus.Selecao.API.Controllers;

using Application.DTOs;
using Application.Queries.ProcessosSeletivos;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Infrastructure.Core.Hateoas;
using Unifesspa.UniPlus.Kernel.Results;

[ApiController]
[Route("processos-seletivos/{id:guid}/indicadores")]
[Authorize]
public sealed class IndicadoresDoProcessoController : ControllerBase
{
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;
    private readonly IResourceLinksBuilder<IndicadoresDoProcessoDto> _construtorDeLinks;

    public IndicadoresDoProcessoController(IQueryBus queryBus, IDomainErrorMapper mapper, IResourceLinksBuilder<IndicadoresDoProcessoDto> construtorDeLinks)
    {
        _queryBus = queryBus;
        _mapper = mapper;
        _construtorDeLinks = construtorDeLinks;
    }

    [HttpGet]
    [EndpointName("ObterIndicadoresDoProcesso")]
    [EndpointSummary("Obtém os indicadores quantitativos do processo seletivo")]
    [EndpointDescription("Retorna inscrições submetidas não canceladas, inscritos distintos e rascunhos do processo seletivo. A consulta exige autenticação e autorização conforme o estado e a unidade do processo.")]
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
        if (resultado.IsFailure)
            return resultado.ToActionResult(_mapper);

        IndicadoresDoProcessoDto indicadores = resultado.Value!;
        IReadOnlyDictionary<string, string> links = _construtorDeLinks.Build(indicadores);
        return Ok(indicadores with { Links = new(links["self"], links["processo"]) });
    }
}
