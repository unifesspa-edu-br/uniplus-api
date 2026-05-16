namespace Unifesspa.UniPlus.Selecao.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Infrastructure.Core.Hateoas;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ObrigatoriedadesLegais;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ObrigatoriedadesLegais;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Admin CRUD + leitura pública de <c>ObrigatoriedadeLegal</c>
/// (Story #461, ADR-0058 Emenda 1). Paths path-based per ADR-0064:
/// público sob <c>/api/selecao/obrigatoriedades-legais</c>, admin sob
/// <c>/api/selecao/admin/obrigatoriedades-legais</c>. RBAC área-scoped
/// (ADR-0057) é verificado pelos handlers via <c>IUserContext.AreasAdministradas</c>
/// — controller mantém apenas a authentication gate.
/// </summary>
[ApiController]
[Route("api/selecao")]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public; sem isso o MVC ignora a classe.")]
public sealed class ObrigatoriedadeLegalController : ControllerBase
{
    private const string ResourceTag = "obrigatoriedades-legais";

    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;
    private readonly IResourceLinksBuilder<ObrigatoriedadeLegalDto> _linksBuilder;

    public ObrigatoriedadeLegalController(
        ICommandBus commandBus,
        IQueryBus queryBus,
        IDomainErrorMapper mapper,
        IResourceLinksBuilder<ObrigatoriedadeLegalDto> linksBuilder)
    {
        _commandBus = commandBus;
        _queryBus = queryBus;
        _mapper = mapper;
        _linksBuilder = linksBuilder;
    }

    /// <summary>
    /// Lista paginada (cursor) com filtros admin. Leitura pública —
    /// <c>vigentes=true</c> por default.
    /// </summary>
    [HttpGet("obrigatoriedades-legais")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "obrigatoriedade-legal", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<ObrigatoriedadeLegalDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Listar(
        [FromCursor(ResourceTag)] PageRequest page,
        [FromQuery] string? tipoEdital,
        [FromQuery] CategoriaObrigatoriedade? categoria,
        [FromQuery] string? proprietario,
        [FromQuery] bool vigentes = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);

        ListarObrigatoriedadesLegaisResult resultado = await _queryBus.Send(
            new ListarObrigatoriedadesLegaisQuery(
                page.AfterId,
                page.Limit,
                tipoEdital,
                categoria,
                proprietario,
                vigentes),
            cancellationToken).ConfigureAwait(false);

        return await this.OkPaginatedAsync(
            resultado.Items,
            resultado.ProximoAfterId,
            page,
            ResourceTag,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Obtém uma regra pelo Id. HATEOAS Level 1 com <c>_links.self/collection</c>.
    /// </summary>
    [HttpGet("obrigatoriedades-legais/{id:guid}")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "obrigatoriedade-legal", Versions = [1])]
    [ProducesResponseType(typeof(ObrigatoriedadeLegalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken cancellationToken)
    {
        ObrigatoriedadeLegalDto? dto = await _queryBus
            .Send(new ObterObrigatoriedadeLegalQuery(id), cancellationToken)
            .ConfigureAwait(false);
        if (dto is null)
        {
            return NotFound();
        }

        ObrigatoriedadeLegalDto comLinks = dto with { Links = _linksBuilder.Build(dto) };
        return Ok(comLinks);
    }

    /// <summary>
    /// Cria uma nova <c>ObrigatoriedadeLegal</c>. Restrito a admin
    /// área-scoped (ADR-0057) — checagem no handler com base no
    /// <c>Proprietario</c> do payload. <c>Idempotency-Key</c> obrigatório
    /// (ADR-0027).
    /// </summary>
    [HttpPost("admin/obrigatoriedades-legais")]
    [Authorize]
    [RequiresIdempotencyKey]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Criar(
        [FromBody] CriarObrigatoriedadeLegalCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        Result<Guid> resultado = await _commandBus
            .Send(command, cancellationToken)
            .ConfigureAwait(false);

        if (resultado.IsSuccess)
        {
            return CreatedAtAction(nameof(ObterPorId), new { id = resultado.Value }, resultado.Value);
        }

        return resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Atualiza in-place (full-replace per ADR-0058 Emenda 1). Caller deve
    /// repassar todos os campos. <c>Idempotency-Key</c> obrigatório.
    /// </summary>
    [HttpPut("admin/obrigatoriedades-legais/{id:guid}")]
    [Authorize]
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
        [FromBody] AtualizarObrigatoriedadeLegalCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.Id != id)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Id do path não bate com Id do body",
                Status = StatusCodes.Status400BadRequest,
                Type = "uniplus.contract.id_path_body_divergente",
            });
        }

        Result resultado = await _commandBus
            .Send(command, cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Desativa (soft-delete) a regra. FK <c>RESTRICT</c> de
    /// <c>obrigatoriedade_legal_historico</c> protege contra hard-delete
    /// acidental; soft-delete (Modified+IsDeleted=true) não dispara RESTRICT
    /// e libera o slot do <c>UNIQUE</c> parcial sobre <c>Hash</c> (ADR-0063).
    /// </summary>
    [HttpDelete("admin/obrigatoriedades-legais/{id:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Desativar(Guid id, CancellationToken cancellationToken)
    {
        Result resultado = await _commandBus
            .Send(new DesativarObrigatoriedadeLegalCommand(id), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }
}
