namespace Unifesspa.UniPlus.Selecao.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Application.DTOs;
using Application.Queries.Acompanhamento;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Infrastructure.Core.OpenApi;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;

[ApiController]
[Route("acompanhamento/processos-seletivos")]
[Authorize]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public.")]
public sealed class AcompanhamentoProcessosSeletivosController : ControllerBase
{
    private const string ResourceTag = "acompanhamento-processos-seletivos";
    private readonly IQueryBus _queryBus;
    private readonly AcompanhamentoOptions _options;

    public AcompanhamentoProcessosSeletivosController(IQueryBus queryBus, IOptions<AcompanhamentoOptions> options)
    {
        ArgumentNullException.ThrowIfNull(queryBus);
        ArgumentNullException.ThrowIfNull(options);
        _queryBus = queryBus;
        _options = options.Value;
    }

    [HttpGet]
    [VendorMediaType(Resource = "acompanhamento-processo-seletivo", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<ProcessoAcompanhamentoResumoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Listar(
        [FromCursor(ResourceTag)] PageRequest page,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (!_options.ValidacaoHabilitada)
            return Forbid();

        ListarProcessosAcompanhamentoResult resultado = await _queryBus.Send(
            new ListarProcessosAcompanhamentoQuery(page.AfterId, page.Limit, page.Direction), cancellationToken);

        return await this.OkPaginatedAsync(
            resultado.Items, resultado.AnteriorAfterId, resultado.ProximoAfterId, page, ResourceTag,
            cancellationToken: cancellationToken);
    }

    [HttpGet("{id:guid}")]
    [VendorMediaType(Resource = "acompanhamento-processo-seletivo", Versions = [1])]
    [ProducesResponseType(typeof(ProcessoAcompanhamentoResumoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken cancellationToken)
    {
        if (!_options.ValidacaoHabilitada)
            return Forbid();

        ProcessoAcompanhamentoResumoDto? processo = await _queryBus.Send(
            new ObterProcessoAcompanhamentoQuery(id), cancellationToken);
        return processo is null ? NotFound() : Ok(processo);
    }
}
