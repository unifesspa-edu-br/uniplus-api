namespace Unifesspa.UniPlus.Configuracao.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.Commands.TermosConsentimento;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Queries.TermosConsentimento;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Infrastructure.Core.Hateoas;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Endpoints públicos de leitura (<c>GET /api/configuracao/termos-consentimento</c>,
/// <c>GET /api/configuracao/termos-consentimento/{id}</c>) e endpoints admin
/// (<c>POST/PUT/DELETE /api/configuracao/admin/termos-consentimento</c>, revisar e
/// promover) restritos a <c>plataforma-admin</c> (UNI-REQ-0086, catálogo — issue #1018).
/// </summary>
[ApiController]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public.")]
public sealed class TermosConsentimentoController : ControllerBase
{
    private const string ResourceTag = "termos-consentimento";

    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;
    private readonly IResourceLinksBuilder<TermoConsentimentoResumoDto> _resumoLinksBuilder;
    private readonly IResourceLinksBuilder<TermoConsentimentoDto> _linksBuilder;

    public TermosConsentimentoController(
        ICommandBus commandBus,
        IQueryBus queryBus,
        IDomainErrorMapper mapper,
        IResourceLinksBuilder<TermoConsentimentoResumoDto> resumoLinksBuilder,
        IResourceLinksBuilder<TermoConsentimentoDto> linksBuilder)
    {
        _commandBus = commandBus;
        _queryBus = queryBus;
        _mapper = mapper;
        _resumoLinksBuilder = resumoLinksBuilder;
        _linksBuilder = linksBuilder;
    }

    /// <summary>
    /// Lista os termos de consentimento vivos, paginados por cursor opaco
    /// bidirecional (ADR-0026 + ADR-0089). Não inclui as versões promovidas —
    /// use <see cref="ObterPorId"/> para o termo completo.
    /// </summary>
    [HttpGet("termos-consentimento")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "termo-consentimento", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<TermoConsentimentoResumoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Listar(
        [FromCursor(ResourceTag)] PageRequest page,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);

        ListarTermosConsentimentoResult resultado = await _queryBus
            .Send(new ListarTermosConsentimentoQuery(page.AfterId, page.Limit, page.Direction), cancellationToken)
            .ConfigureAwait(false);

        TermoConsentimentoResumoDto[] comLinks =
            [.. resultado.Items.Select(t => t with { Links = _resumoLinksBuilder.Build(t) })];

        return await this.OkPaginatedAsync(
            comLinks, resultado.AnteriorAfterId, resultado.ProximoAfterId, page, ResourceTag,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Obtém um termo pelo Id, com a lista completa de versões promovidas. Retorna 404 quando inexistente.</summary>
    [HttpGet("termos-consentimento/{id:guid}")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "termo-consentimento", Versions = [1])]
    [ProducesResponseType(typeof(TermoConsentimentoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken cancellationToken)
    {
        TermoConsentimentoDto? termo = await _queryBus
            .Send(new ObterTermoConsentimentoPorIdQuery(id), cancellationToken)
            .ConfigureAwait(false);

        if (termo is null)
        {
            return NotFound();
        }

        TermoConsentimentoDto comLinks = termo with { Links = _linksBuilder.Build(termo) };
        return Ok(comLinks);
    }

    /// <summary>Cria um novo termo (rascunho vazio ou com campos iniciais). Restrito a <c>plataforma-admin</c>. Idempotency-Key obrigatório (ADR-0027).</summary>
    [HttpPost("admin/termos-consentimento")]
    [Authorize(Roles = "plataforma-admin")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Criar(
        [FromBody] CriarTermoConsentimentoCommand command,
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

    /// <summary>
    /// Edita o rascunho corrente (texto, base legal, forma de aceite). Se o
    /// rascunho já estava revisado, a edição reverte o status para
    /// <c>EM_ELABORACAO</c> e limpa a marca de revisão. Restrito a <c>plataforma-admin</c>.
    /// </summary>
    [HttpPut("admin/termos-consentimento/{id:guid}/rascunho")]
    [Authorize(Roles = "plataforma-admin")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> EditarRascunho(
        Guid id,
        [FromBody] EditarRascunhoTermoConsentimentoCommand command,
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

    /// <summary>
    /// Marca o rascunho corrente como revisado — portão distinto da edição
    /// comum, grava o ator explícito da chamada. Restrito a <c>plataforma-admin</c>.
    /// </summary>
    [HttpPost("admin/termos-consentimento/{id:guid}/revisar")]
    [Authorize(Roles = "plataforma-admin")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> MarcarRevisado(Guid id, CancellationToken cancellationToken)
    {
        Result resultado = await _commandBus
            .Send(new MarcarRevisadoTermoConsentimentoCommand(id), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Promove o rascunho revisado a uma nova versão imutável. Restrito a
    /// <c>plataforma-admin</c>.
    /// </summary>
    [HttpPost("admin/termos-consentimento/{id:guid}/promover")]
    [Authorize(Roles = "plataforma-admin")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Promover(Guid id, CancellationToken cancellationToken)
    {
        Result resultado = await _commandBus
            .Send(new PromoverVersaoTermoConsentimentoCommand(id), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }

    /// <summary>Remove (soft-delete) um termo sem nenhuma versão promovida. Restrito a <c>plataforma-admin</c>.</summary>
    [HttpDelete("admin/termos-consentimento/{id:guid}")]
    [Authorize(Roles = "plataforma-admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Remover(Guid id, CancellationToken cancellationToken)
    {
        Result resultado = await _commandBus
            .Send(new RemoverTermoConsentimentoCommand(id), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }
}
