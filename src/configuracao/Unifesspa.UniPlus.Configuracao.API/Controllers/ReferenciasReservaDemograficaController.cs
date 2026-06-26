namespace Unifesspa.UniPlus.Configuracao.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.Commands.ReferenciasReservaDemografica;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Queries.ReferenciasReservaDemografica;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Infrastructure.Core.Hateoas;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Endpoints públicos de leitura (<c>GET /api/configuracao/referencias-reserva-demografica</c>,
/// <c>GET /api/configuracao/referencias-reserva-demografica/{id}</c>) e endpoints admin
/// (<c>POST/PUT/DELETE /api/configuracao/admin/referencias-reserva-demografica</c>) restritos a
/// <c>plataforma-admin</c> (UNI-REQ-0065).
/// </summary>
[ApiController]
[Route("api/configuracao")]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public.")]
public sealed class ReferenciasReservaDemograficaController : ControllerBase
{
    private const string ResourceTag = "referencias-reserva-demografica";

    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;
    private readonly IResourceLinksBuilder<ReferenciaReservaDemograficaDto> _linksBuilder;

    public ReferenciasReservaDemograficaController(
        ICommandBus commandBus,
        IQueryBus queryBus,
        IDomainErrorMapper mapper,
        IResourceLinksBuilder<ReferenciaReservaDemograficaDto> linksBuilder)
    {
        _commandBus = commandBus;
        _queryBus = queryBus;
        _mapper = mapper;
        _linksBuilder = linksBuilder;
    }

    /// <summary>
    /// Lista as referências de reserva demográfica ativas, paginadas por cursor
    /// opaco bidirecional (ADR-0026 + ADR-0089). Navegação via header <c>Link</c>;
    /// cada item carrega seu <c>_links.self</c> (ADR-0029).
    /// </summary>
    [HttpGet("referencias-reserva-demografica")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "referencia-reserva-demografica", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<ReferenciaReservaDemograficaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Listar(
        [FromCursor(ResourceTag)] PageRequest page,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);

        ListarReferenciasReservaDemograficaResult resultado = await _queryBus
            .Send(new ListarReferenciasReservaDemograficaQuery(page.AfterId, page.Limit, page.Direction), cancellationToken)
            .ConfigureAwait(false);

        ReferenciaReservaDemograficaDto[] comLinks =
            [.. resultado.Items.Select(r => r with { Links = _linksBuilder.Build(r) })];

        return await this.OkPaginatedAsync(
            comLinks, resultado.AnteriorAfterId, resultado.ProximoAfterId, page, ResourceTag,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Obtém uma referência pelo Id. Retorna 404 quando inexistente.</summary>
    [HttpGet("referencias-reserva-demografica/{id:guid}")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "referencia-reserva-demografica", Versions = [1])]
    [ProducesResponseType(typeof(ReferenciaReservaDemograficaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken cancellationToken)
    {
        ReferenciaReservaDemograficaDto? referencia = await _queryBus
            .Send(new ObterReferenciaReservaDemograficaPorIdQuery(id), cancellationToken)
            .ConfigureAwait(false);

        if (referencia is null)
        {
            return NotFound();
        }

        ReferenciaReservaDemograficaDto comLinks = referencia with { Links = _linksBuilder.Build(referencia) };
        return Ok(comLinks);
    }

    /// <summary>Cria uma nova referência. Restrito a <c>plataforma-admin</c>. Idempotency-Key obrigatório (ADR-0027).</summary>
    [HttpPost("admin/referencias-reserva-demografica")]
    [Authorize(Roles = "plataforma-admin")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Criar(
        [FromBody] CriarReferenciaReservaDemograficaCommand command,
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

    /// <summary>Atualiza uma referência existente. Restrito a <c>plataforma-admin</c>.</summary>
    [HttpPut("admin/referencias-reserva-demografica/{id:guid}")]
    [Authorize(Roles = "plataforma-admin")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Atualizar(
        Guid id,
        [FromBody] AtualizarReferenciaReservaDemograficaCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (id != command.Id)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Id divergente",
                Detail = "O Id na URL não corresponde ao Id no corpo da requisição.",
                Status = StatusCodes.Status400BadRequest,
            });
        }

        Result resultado = await _commandBus
            .Send(command, cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }

    /// <summary>Remove (soft-delete) uma referência. Restrito a <c>plataforma-admin</c>.</summary>
    [HttpDelete("admin/referencias-reserva-demografica/{id:guid}")]
    [Authorize(Roles = "plataforma-admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remover(Guid id, CancellationToken cancellationToken)
    {
        Result resultado = await _commandBus
            .Send(new RemoverReferenciaReservaDemograficaCommand(id), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }
}
