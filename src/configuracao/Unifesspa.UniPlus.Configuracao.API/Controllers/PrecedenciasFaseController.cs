namespace Unifesspa.UniPlus.Configuracao.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.Commands.PrecedenciasFase;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Queries.PrecedenciasFase;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Infrastructure.Core.Hateoas;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Endpoints públicos de leitura (<c>GET /api/configuracao/precedencias-fase</c>,
/// <c>GET /api/configuracao/precedencias-fase/{id}</c>) e endpoints admin
/// (<c>POST/PUT/DELETE /api/configuracao/admin/precedencias-fase</c>) restritos a
/// <c>plataforma-admin</c> (UNI-REQ-0064) — espelha <see cref="FasesCanonicasController"/>
/// em rota, autenticação, HATEOAS, vendor media type e idempotência.
/// </summary>
[ApiController]
[Route("api/configuracao")]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public.")]
public sealed class PrecedenciasFaseController : ControllerBase
{
    private const string ResourceTag = "precedencias-fase";

    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;
    private readonly IResourceLinksBuilder<PrecedenciaFaseDto> _linksBuilder;

    public PrecedenciasFaseController(
        ICommandBus commandBus,
        IQueryBus queryBus,
        IDomainErrorMapper mapper,
        IResourceLinksBuilder<PrecedenciaFaseDto> linksBuilder)
    {
        _commandBus = commandBus;
        _queryBus = queryBus;
        _mapper = mapper;
        _linksBuilder = linksBuilder;
    }

    /// <summary>
    /// Lista as arestas de precedência vivas, paginadas por cursor opaco
    /// bidirecional (ADR-0026 + ADR-0089). Navegação via header <c>Link</c>; cada
    /// item carrega seu <c>_links.self</c> (ADR-0029).
    /// </summary>
    [HttpGet("precedencias-fase")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "precedencia-fase", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<PrecedenciaFaseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Listar(
        [FromCursor(ResourceTag)] PageRequest page,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);

        ListarPrecedenciasFaseResult resultado = await _queryBus
            .Send(new ListarPrecedenciasFaseQuery(page.AfterId, page.Limit, page.Direction), cancellationToken)
            .ConfigureAwait(false);

        PrecedenciaFaseDto[] comLinks =
            [.. resultado.Items.Select(p => p with { Links = _linksBuilder.Build(p) })];

        return await this.OkPaginatedAsync(
            comLinks, resultado.AnteriorAfterId, resultado.ProximoAfterId, page, ResourceTag,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Obtém uma aresta de precedência pelo Id. Retorna 404 quando inexistente.</summary>
    [HttpGet("precedencias-fase/{id:guid}")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "precedencia-fase", Versions = [1])]
    [ProducesResponseType(typeof(PrecedenciaFaseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken cancellationToken)
    {
        PrecedenciaFaseDto? aresta = await _queryBus
            .Send(new ObterPrecedenciaFasePorIdQuery(id), cancellationToken)
            .ConfigureAwait(false);

        if (aresta is null)
        {
            return NotFound();
        }

        PrecedenciaFaseDto comLinks = aresta with { Links = _linksBuilder.Build(aresta) };
        return Ok(comLinks);
    }

    /// <summary>Cria uma nova aresta de precedência. Restrito a <c>plataforma-admin</c>. Idempotency-Key obrigatório (ADR-0027).</summary>
    [HttpPost("admin/precedencias-fase")]
    [Authorize(Roles = "plataforma-admin")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Criar(
        [FromBody] CriarPrecedenciaFaseCommand command,
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

    /// <summary>Atualiza a permissão de sobreposição de uma aresta existente. Restrito a <c>plataforma-admin</c>.</summary>
    [HttpPut("admin/precedencias-fase/{id:guid}")]
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
        [FromBody] AtualizarPrecedenciaFaseCommand command,
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

    /// <summary>Remove (soft-delete) uma aresta de precedência. Restrito a <c>plataforma-admin</c>.</summary>
    [HttpDelete("admin/precedencias-fase/{id:guid}")]
    [Authorize(Roles = "plataforma-admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remover(Guid id, CancellationToken cancellationToken)
    {
        Result resultado = await _commandBus
            .Send(new RemoverPrecedenciaFaseCommand(id), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }
}
