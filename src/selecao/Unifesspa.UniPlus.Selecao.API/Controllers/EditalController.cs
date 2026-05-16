namespace Unifesspa.UniPlus.Selecao.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Infrastructure.Core.Hateoas;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Results;
using Application.Commands.Editais;
using Application.DTOs;
using Application.Queries.Editais;
using Application.Queries.ObrigatoriedadesLegais;

[ApiController]
[Route("api/selecao/editais")]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public; sem isso o MVC ignora a classe e nenhum endpoint é registrado.")]
public sealed class EditalController : ControllerBase
{
    private const string ResourceTag = "editais";

    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;
    private readonly IResourceLinksBuilder<EditalDto> _linksBuilder;

    public EditalController(
        ICommandBus commandBus,
        IQueryBus queryBus,
        IDomainErrorMapper mapper,
        IResourceLinksBuilder<EditalDto> linksBuilder)
    {
        _commandBus = commandBus;
        _queryBus = queryBus;
        _mapper = mapper;
        _linksBuilder = linksBuilder;
    }

    [HttpPost]
    [RequiresIdempotencyKey]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Criar([FromBody] CriarEditalCommand command, CancellationToken cancellationToken)
    {
        Result<Guid> resultado = await _commandBus.Send(command, cancellationToken);
        if (resultado.IsSuccess)
            return CreatedAtAction(nameof(ObterPorId), new { id = resultado.Value }, resultado.Value);
        return resultado.ToActionResult(_mapper);
    }

    [HttpGet]
    [VendorMediaType(Resource = "edital", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<EditalDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Listar(
        [FromCursor(ResourceTag)] PageRequest page,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);

        ListarEditaisResult resultado = await _queryBus.Send(
            new ListarEditaisQuery(page.AfterId, page.Limit), cancellationToken);

        return await this.OkPaginatedAsync(
            resultado.Items, resultado.ProximoAfterId, page, ResourceTag,
            cancellationToken: cancellationToken);
    }

    [HttpGet("{id:guid}")]
    [VendorMediaType(Resource = "edital", Versions = [1])]
    [ProducesResponseType(typeof(EditalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken cancellationToken)
    {
        EditalDto? edital = await _queryBus.Send(new ObterEditalQuery(id), cancellationToken);
        if (edital is null)
        {
            return NotFound();
        }

        // HATEOAS Level 1 (ADR-0029) — anexa _links.self/_links.collection
        // ao DTO. O builder não toca em domínio; opera sobre URIs relativas
        // via LinkGenerator. Action links (publicar etc.) NÃO entram aqui —
        // descobertos via OpenAPI (ADR-0030 + ADR-0029 §"Esta ADR não decide").
        EditalDto editalComLinks = edital with { Links = _linksBuilder.Build(edital) };
        return Ok(editalComLinks);
    }

    // Despacha PublicarEditalCommand pelo ICommandBus (Wolverine). O handler
    // convention-based atualiza o agregado e drena EditalPublicadoEvent por
    // cascading messages — atomicidade write+evento garantida pela
    // IEnvelopeTransaction da configuração produtiva (ADR-0005).
    [HttpPost("{id:guid}/publicar")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Publicar(Guid id, CancellationToken cancellationToken)
    {
        Result resultado = await _commandBus.Send(new PublicarEditalCommand(id), cancellationToken);
        if (resultado.IsSuccess)
            return NoContent();
        return resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Conformidade atual: avalia <c>ValidadorConformidadeEdital</c> contra
    /// o ruleset vigente filtrado por tipo de edital, retornando lista de
    /// regras com veredicto <c>Aprovada/Reprovada</c>. Consumido pelo wizard
    /// no passo de revisão (ADR-0058). HATEOAS <c>_links.self</c> sempre
    /// presente.
    /// </summary>
    [HttpGet("{id:guid}/conformidade")]
    [VendorMediaType(Resource = "conformidade", Versions = [1])]
    [ProducesResponseType(typeof(ConformidadeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ObterConformidade(Guid id, CancellationToken cancellationToken)
    {
        ConformidadeDto? conformidade = await _queryBus
            .Send(new ObterConformidadeAtualQuery(id), cancellationToken)
            .ConfigureAwait(false);
        if (conformidade is null)
        {
            return NotFound();
        }

        return Ok(conformidade with
        {
            Links = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["self"] = Url.Action(
                    nameof(ObterConformidade),
                    "Edital",
                    new { id })
                    ?? $"/api/selecao/editais/{id}/conformidade",
            },
        });
    }

    /// <summary>
    /// Conformidade histórica: retorna o snapshot imutável persistido em
    /// <c>edital_governance_snapshot.regras_json</c> no momento de
    /// <c>Edital.Publicar()</c>. Retorna <c>404 Not Found</c> com
    /// <c>uniplus.selecao.conformidade.snapshot_nao_disponivel</c> quando
    /// o edital ainda não foi publicado (snapshot inexistente — preenchimento
    /// pela Story #462).
    /// </summary>
    [HttpGet("{id:guid}/conformidade-historica")]
    [VendorMediaType(Resource = "conformidade", Versions = [1])]
    [ProducesResponseType(typeof(ConformidadeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ObterConformidadeHistorica(Guid id, CancellationToken cancellationToken)
    {
        ConformidadeDto? historico = await _queryBus
            .Send(new ObterConformidadeHistoricaQuery(id), cancellationToken)
            .ConfigureAwait(false);
        if (historico is null)
        {
            // ProblemDetails específico (ADR-0023): type matching com o code
            // registrado em SelecaoDomainErrorRegistration. Não construímos
            // DomainError aqui — controllers não dependem dele (fitness test
            // F3 — ADR-0024).
            return NotFound(new ProblemDetails
            {
                Title = "Snapshot de conformidade indisponível",
                Status = StatusCodes.Status404NotFound,
                Type = "uniplus.selecao.conformidade.snapshot_nao_disponivel",
                Detail = $"Snapshot de conformidade indisponível para o edital {id} — "
                    + "o edital pode estar em rascunho ou nunca ter sido publicado.",
            });
        }

        return Ok(historico with
        {
            Links = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["self"] = Url.Action(
                    nameof(ObterConformidadeHistorica),
                    "Edital",
                    new { id })
                    ?? $"/api/selecao/editais/{id}/conformidade-historica",
            },
        });
    }
}
