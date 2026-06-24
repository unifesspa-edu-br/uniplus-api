namespace Unifesspa.UniPlus.Configuracao.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.Commands.PesosAreaEnem;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Queries.PesosAreaEnem;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Infrastructure.Core.Hateoas;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Endpoints públicos de leitura (<c>GET /api/pesos-area-enem</c>,
/// <c>GET /api/pesos-area-enem/{id}</c>) e endpoints admin
/// (<c>POST/PUT/DELETE /api/admin/pesos-area-enem</c>) restritos a
/// <c>plataforma-admin</c> (UNI-REQ-0066).
/// </summary>
[ApiController]
[Route("api")]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public.")]
public sealed class PesosAreaEnemController : ControllerBase
{
    private const string ResourceTag = "pesos-area-enem";

    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;
    private readonly IResourceLinksBuilder<PesoAreaEnemDto> _linksBuilder;

    public PesosAreaEnemController(
        ICommandBus commandBus,
        IQueryBus queryBus,
        IDomainErrorMapper mapper,
        IResourceLinksBuilder<PesoAreaEnemDto> linksBuilder)
    {
        _commandBus = commandBus;
        _queryBus = queryBus;
        _mapper = mapper;
        _linksBuilder = linksBuilder;
    }

    /// <summary>
    /// Lista as linhas de pesos do ENEM ativas, paginadas por cursor opaco
    /// bidirecional (ADR-0026 + ADR-0089). Navegação via header <c>Link</c>; cada
    /// item carrega seu <c>_links.self</c> (ADR-0029).
    /// </summary>
    [HttpGet("pesos-area-enem")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "peso-area-enem", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<PesoAreaEnemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Listar(
        [FromCursor(ResourceTag)] PageRequest page,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);

        ListarPesosAreaEnemResult resultado = await _queryBus
            .Send(new ListarPesosAreaEnemQuery(page.AfterId, page.Limit, page.Direction), cancellationToken)
            .ConfigureAwait(false);

        PesoAreaEnemDto[] comLinks =
            [.. resultado.Items.Select(p => p with { Links = _linksBuilder.Build(p) })];

        return await this.OkPaginatedAsync(
            comLinks, resultado.AnteriorAfterId, resultado.ProximoAfterId, page, ResourceTag,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Obtém uma linha de pesos pelo Id. Retorna 404 quando inexistente.</summary>
    [HttpGet("pesos-area-enem/{id:guid}")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "peso-area-enem", Versions = [1])]
    [ProducesResponseType(typeof(PesoAreaEnemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken cancellationToken)
    {
        PesoAreaEnemDto? peso = await _queryBus
            .Send(new ObterPesoAreaEnemPorIdQuery(id), cancellationToken)
            .ConfigureAwait(false);

        if (peso is null)
        {
            return NotFound();
        }

        PesoAreaEnemDto comLinks = peso with { Links = _linksBuilder.Build(peso) };
        return Ok(comLinks);
    }

    /// <summary>Cria uma nova linha de pesos. Restrito a <c>plataforma-admin</c>. Idempotency-Key obrigatório (ADR-0027).</summary>
    [HttpPost("admin/pesos-area-enem")]
    [Authorize(Roles = "plataforma-admin")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Criar(
        [FromBody] CriarPesoAreaEnemCommand command,
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

    /// <summary>Atualiza uma linha de pesos existente. Restrito a <c>plataforma-admin</c>.</summary>
    [HttpPut("admin/pesos-area-enem/{id:guid}")]
    [Authorize(Roles = "plataforma-admin")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Atualizar(
        Guid id,
        [FromBody] AtualizarPesoAreaEnemCommand command,
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

    /// <summary>Remove (soft-delete) uma linha de pesos. Restrito a <c>plataforma-admin</c>.</summary>
    [HttpDelete("admin/pesos-area-enem/{id:guid}")]
    [Authorize(Roles = "plataforma-admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remover(Guid id, CancellationToken cancellationToken)
    {
        Result resultado = await _commandBus
            .Send(new RemoverPesoAreaEnemCommand(id), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }
}
