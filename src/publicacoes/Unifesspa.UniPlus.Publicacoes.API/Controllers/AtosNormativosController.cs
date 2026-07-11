namespace Unifesspa.UniPlus.Publicacoes.API.Controllers;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;

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
public sealed partial class AtosNormativosController : ControllerBase
{
    private const string ResourceTag = "atos";
    private const string ResourceTagEntidade = "entidade-atos";
    private const string RouteEntidadeTipo = "entidadeTipo";
    private const string RouteEntidadeId = "entidadeId";

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
    /// Lista os atos publicados que tratam de uma entidade — a consulta unificada
    /// (ADR-0105): todos os atos de um certame num só lugar, em ordem cronológica de
    /// publicação.
    /// </summary>
    /// <remarks>
    /// <para>O par <c>(entidadeTipo, entidadeId)</c> é opaco para o módulo, que não
    /// conhece os domínios e por isso <b>não sabe se a entidade existe</b>. Entidade
    /// inexistente e entidade sem ato algum devolvem, ambas, uma coleção vazia — nunca
    /// 404. Distingui-las exigiria perguntar a outro módulo, e é justamente essa pergunta
    /// que a fronteira proíbe.</para>
    /// <para>Ordem: data de publicação ascendente, com o <c>Id</c> (Guid v7) de
    /// desempate — a retificação republica a mesma data. Paginação por cursor opaco
    /// escopado à entidade: um cursor emitido aqui não navega a coleção de outra
    /// entidade.</para>
    /// </remarks>
    [HttpGet("entidades/{entidadeTipo}/{entidadeId:guid}/atos")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "ato-normativo", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<AtoNormativoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ListarDaEntidade(
        string entidadeTipo,
        Guid entidadeId,
        [FromCursor(
            ResourceTagEntidade,
            RequireSortKey = true,
            ScopeRouteValues = [RouteEntidadeTipo, RouteEntidadeId])] PageRequest page,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);

        // A forma do rótulo é conferida — o valor, jamais. Sem esta recusa, um rótulo
        // fora da grafia canônica (minúsculo, com hífen) devolveria a mesma coleção vazia
        // de uma entidade sem atos, e o erro de digitação passaria por resultado legítimo.
        if (!FormatoDoTipoDeEntidade().IsMatch(entidadeTipo))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Tipo de entidade inválido",
                detail: "O tipo da entidade deve ser um rótulo em maiúsculas, com grupos separados por sublinhado.");
        }

        // A âncora do cursor é o par (data de publicação, Id), e a data chega como texto.
        // Decodificar o wire é do boundary (ADR-0031): sem esta recusa, uma âncora
        // malformada só falharia lá dentro, ao construir a chave do seek — e um cursor
        // adulterado viraria 500 em vez de 400.
        if (page.AfterSortKey is { } ancora
            && !DateOnly.TryParseExact(ancora, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Cursor inválido",
                detail: "O cursor informado é inválido.");
        }

        ListarAtosDaEntidadeResult resultado = await _queryBus
            .Send(
                new ListarAtosDaEntidadeQuery(
                    entidadeTipo, entidadeId, page.AfterSortKey, page.AfterId, page.Limit, page.Direction),
                cancellationToken)
            .ConfigureAwait(false);

        AtoNormativoDto[] comLinks =
            [.. resultado.Items.Select(a => a with { Links = _linksBuilder.Build(a) })];

        return await this.OkPaginatedOrdenadoAsync(
            comLinks,
            resultado.Anterior,
            resultado.Proximo,
            page,
            EtiquetaDaEntidade(entidadeTipo, entidadeId),
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
    /// Registra um ato publicado — publicação nova ou retificação de outro ato,
    /// quando o corpo traz o par <c>atoRetificadoId</c>/<c>motivoRetificacao</c>
    /// (ADR-0103). Restrito a <c>plataforma-admin</c>. <c>Idempotency-Key</c>
    /// obrigatório (ADR-0027). A resposta traz o Id, o instante forense de registro
    /// e eventuais avisos de numeração (AC4). Uma retificação que quebra a cadeia
    /// linear (o ato-alvo já foi retificado) responde 409.
    /// </summary>
    [HttpPost("admin/atos")]
    [Authorize(Roles = "plataforma-admin")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(typeof(RegistrarAtoNormativoResult), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
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

    /// <summary>
    /// Etiqueta do cursor da coleção desta entidade. Composta pelo mesmo utilitário que
    /// o binder usa para conferi-la — se cada lado a montasse à sua maneira, nenhum
    /// cursor emitido aqui passaria na leitura.
    /// </summary>
    private static string EtiquetaDaEntidade(string entidadeTipo, Guid entidadeId) =>
        CursorResourceTag.Compose(
            ResourceTagEntidade, [entidadeTipo, entidadeId.ToString()]);

    /// <summary>Grafia canônica do rótulo opaco da entidade (a mesma que o domínio exige).</summary>
    [GeneratedRegex(@"^[A-Z0-9]+(_[A-Z0-9]+)*$")]
    private static partial Regex FormatoDoTipoDeEntidade();
}
