namespace Unifesspa.UniPlus.Publicacoes.API.Controllers;

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
using Unifesspa.UniPlus.Publicacoes.Application.Commands.AtosNormativos;
using Unifesspa.UniPlus.Publicacoes.Application.DTOs;
using Unifesspa.UniPlus.Publicacoes.Application.Queries.AtosNormativos;

/// <summary>
/// Endpoints públicos de leitura (<c>GET /api/publicacoes/atos</c>) e o endpoint
/// admin de registro (<c>POST /api/publicacoes/admin/atos</c>) restrito a
/// <c>plataforma-admin</c>.
/// </summary>
/// <remarks>
/// A leitura é pública: um ato publicado é, por definição, um documento tornado
/// público. O <c>assinante</c> é dado pessoal (nome), porém de publicação
/// legítima no próprio ato — não é PII sensível a mascarar, e sim informação que
/// o documento já torna pública. O registro é append-only; não há atualização nem
/// remoção via HTTP (o ato é prova, o passado documental não se muta).
/// </remarks>
[ApiController]
[Route("api/publicacoes")]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public.")]
public sealed class AtosNormativosController : ControllerBase
{
    private const string ResourceTag = "atos";

    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;
    private readonly IResourceLinksBuilder<AtoNormativoDto> _linksBuilder;

    public AtosNormativosController(
        ICommandBus commandBus,
        IQueryBus queryBus,
        IDomainErrorMapper mapper,
        IResourceLinksBuilder<AtoNormativoDto> linksBuilder)
    {
        _commandBus = commandBus;
        _queryBus = queryBus;
        _mapper = mapper;
        _linksBuilder = linksBuilder;
    }

    /// <summary>
    /// Lista os atos publicados, paginados por cursor opaco bidirecional
    /// (ADR-0026 + ADR-0089). Navegação via header <c>Link</c>; cada item carrega
    /// seu <c>_links.self</c> (ADR-0029). Os itens não trazem avisos de numeração —
    /// esses são recomputados só no detalhe do ato.
    /// </summary>
    [HttpGet("atos")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "ato-normativo", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<AtoNormativoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Listar(
        [FromCursor(ResourceTag)] PageRequest page,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);

        ListarAtosNormativosResult resultado = await _queryBus
            .Send(new ListarAtosNormativosQuery(page.AfterId, page.Limit, page.Direction), cancellationToken)
            .ConfigureAwait(false);

        AtoNormativoDto[] comLinks =
            [.. resultado.Items.Select(a => a with { Links = _linksBuilder.Build(a) })];

        return await this.OkPaginatedAsync(
            comLinks, resultado.AnteriorAfterId, resultado.ProximoAfterId, page, ResourceTag,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Obtém um ato pelo Id, com os avisos de numeração recomputados (AC4). Retorna
    /// 404 quando inexistente.
    /// </summary>
    [HttpGet("atos/{id:guid}")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "ato-normativo", Versions = [1])]
    [ProducesResponseType(typeof(AtoNormativoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken cancellationToken)
    {
        AtoNormativoDto? ato = await _queryBus
            .Send(new ObterAtoNormativoPorIdQuery(id), cancellationToken)
            .ConfigureAwait(false);

        if (ato is null)
        {
            return NotFound();
        }

        return Ok(ato with { Links = _linksBuilder.Build(ato) });
    }

    /// <summary>
    /// Registra um ato publicado. Restrito a <c>plataforma-admin</c>.
    /// <c>Idempotency-Key</c> obrigatório (ADR-0027). A resposta traz o Id, o
    /// instante forense de registro e eventuais avisos de numeração (AC4).
    /// </summary>
    [HttpPost("admin/atos")]
    [Authorize(Roles = "plataforma-admin")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(typeof(RegistrarAtoNormativoResult), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Registrar(
        [FromBody] RegistrarAtoNormativoCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        Result<RegistrarAtoNormativoResult> resultado = await _commandBus
            .Send(command, cancellationToken)
            .ConfigureAwait(false);

        if (resultado.IsSuccess)
        {
            return CreatedAtAction(
                nameof(ObterPorId), new { id = resultado.Value!.AtoId }, resultado.Value);
        }

        return resultado.ToActionResult(_mapper);
    }
}
