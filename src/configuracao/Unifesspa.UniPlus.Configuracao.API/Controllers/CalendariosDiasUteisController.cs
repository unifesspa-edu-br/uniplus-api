namespace Unifesspa.UniPlus.Configuracao.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.Commands.CalendariosDiasUteis;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Queries.CalendariosDiasUteis;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Infrastructure.Core.Hateoas;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Endpoints públicos de leitura (<c>GET /api/configuracao/calendarios-dias-uteis</c>,
/// <c>GET /api/configuracao/calendarios-dias-uteis/{id}</c>) e endpoints admin
/// (<c>POST/DELETE /api/configuracao/admin/calendarios-dias-uteis</c>,
/// <c>POST .../{id}/vigente</c>) restritos a <c>plataforma-admin</c> (UNI-REQ-0116).
/// </summary>
[ApiController]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public.")]
public sealed class CalendariosDiasUteisController : ControllerBase
{
    private const string ResourceTag = "calendarios-dias-uteis";

    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;
    private readonly IResourceLinksBuilder<CalendarioDiasUteisResumoDto> _resumoLinksBuilder;
    private readonly IResourceLinksBuilder<CalendarioDiasUteisDto> _linksBuilder;

    public CalendariosDiasUteisController(
        ICommandBus commandBus,
        IQueryBus queryBus,
        IDomainErrorMapper mapper,
        IResourceLinksBuilder<CalendarioDiasUteisResumoDto> resumoLinksBuilder,
        IResourceLinksBuilder<CalendarioDiasUteisDto> linksBuilder)
    {
        _commandBus = commandBus;
        _queryBus = queryBus;
        _mapper = mapper;
        _resumoLinksBuilder = resumoLinksBuilder;
        _linksBuilder = linksBuilder;
    }

    /// <summary>
    /// Lista os datasets de calendário de dias úteis vivos, paginados por cursor
    /// opaco bidirecional (ADR-0026 + ADR-0089). Não inclui os dias não úteis —
    /// use <see cref="ObterPorId"/> para o dataset completo.
    /// </summary>
    [HttpGet("calendarios-dias-uteis")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "calendario-dias-uteis", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<CalendarioDiasUteisResumoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Listar(
        [FromCursor(ResourceTag)] PageRequest page,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);

        ListarCalendariosDiasUteisResult resultado = await _queryBus
            .Send(new ListarCalendariosDiasUteisQuery(page.AfterId, page.Limit, page.Direction), cancellationToken)
            .ConfigureAwait(false);

        CalendarioDiasUteisResumoDto[] comLinks =
            [.. resultado.Items.Select(c => c with { Links = _resumoLinksBuilder.Build(c) })];

        return await this.OkPaginatedAsync(
            comLinks, resultado.AnteriorAfterId, resultado.ProximoAfterId, page, ResourceTag,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Obtém um dataset pelo Id, com a lista completa de dias não úteis. Retorna 404 quando inexistente.</summary>
    [HttpGet("calendarios-dias-uteis/{id:guid}")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "calendario-dias-uteis", Versions = [1])]
    [ProducesResponseType(typeof(CalendarioDiasUteisDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken cancellationToken)
    {
        CalendarioDiasUteisDto? calendario = await _queryBus
            .Send(new ObterCalendarioDiasUteisPorIdQuery(id), cancellationToken)
            .ConfigureAwait(false);

        if (calendario is null)
        {
            return NotFound();
        }

        CalendarioDiasUteisDto comLinks = calendario with { Links = _linksBuilder.Build(calendario) };
        return Ok(comLinks);
    }

    /// <summary>Cria um novo dataset (nasce não vigente). Restrito a <c>plataforma-admin</c>. Idempotency-Key obrigatório (ADR-0027).</summary>
    [HttpPost("admin/calendarios-dias-uteis")]
    [Authorize(Roles = "plataforma-admin")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Criar(
        [FromBody] CriarCalendarioDiasUteisCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        Result<Guid> resultado = await _commandBus
            .Send(command, cancellationToken)
            .ConfigureAwait(false);

        if (resultado.IsSuccess)
        {
            return CreatedAtAction(
                nameof(ObterPorId),
                new { id = resultado.Value },
                resultado.Value);
        }

        return resultado.ToActionResult(_mapper);
    }

    /// <summary>Torna o dataset o vigente, desmarcando o anterior. Restrito a <c>plataforma-admin</c>.</summary>
    [HttpPost("admin/calendarios-dias-uteis/{id:guid}/vigente")]
    [Authorize(Roles = "plataforma-admin")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> MarcarVigente(Guid id, CancellationToken cancellationToken)
    {
        Result resultado = await _commandBus
            .Send(new MarcarVigenteCalendarioDiasUteisCommand(id), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }

    /// <summary>Remove (soft-delete) um dataset não vigente. Restrito a <c>plataforma-admin</c>.</summary>
    [HttpDelete("admin/calendarios-dias-uteis/{id:guid}")]
    [Authorize(Roles = "plataforma-admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Remover(Guid id, CancellationToken cancellationToken)
    {
        Result resultado = await _commandBus
            .Send(new RemoverCalendarioDiasUteisCommand(id), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }
}
