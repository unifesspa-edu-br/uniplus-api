namespace Unifesspa.UniPlus.Publicacoes.API.Controllers;

using System.Globalization;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Infrastructure.Core.Authorization;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Infrastructure.Core.Hateoas;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Application.Commands.AtosNormativos;
using Unifesspa.UniPlus.Publicacoes.Application.DTOs;
using Unifesspa.UniPlus.Publicacoes.Application.Permissoes;
using Unifesspa.UniPlus.Publicacoes.Application.Queries.AtosNormativos;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;
using Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;

/// <summary>
/// Endpoints públicos de leitura (<c>GET /api/publicacoes/atos</c>) e o endpoint
/// admin de registro (<c>POST /api/publicacoes/admin/atos</c>) restrito à
/// permissão de registro.
/// </summary>
/// <remarks>
/// A leitura é pública: um ato publicado é, por definição, um documento tornado
/// público. O <c>assinante</c> é dado pessoal (nome), porém de publicação
/// legítima no próprio ato — não é PII sensível a mascarar, e sim informação que
/// o documento já torna pública. O registro é append-only; não há atualização nem
/// remoção via HTTP (o ato é prova, o passado documental não se muta).
/// </remarks>
[ApiController]
[Authorize]
public sealed class AtosNormativosController : ControllerBase
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
    [EndpointName("ListarAtosNormativos")]
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

        return await this.OkPaginatedAsync(
            ComLinks(resultado.Items), resultado.AnteriorAfterId, resultado.ProximoAfterId, page, ResourceTag,
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
    /// <para>Ordem: instante da divulgação (<c>publicadoEm</c>) ascendente, com o <c>Id</c>
    /// (Guid v7) de desempate para o mesmo microssegundo. Paginação por cursor opaco
    /// escopado à entidade: um cursor emitido aqui não navega a coleção de outra
    /// entidade.</para>
    /// </remarks>
    [HttpGet("entidades/{entidadeTipo}/{entidadeId:guid}/atos")]
    [EndpointName("ListarAtosNormativosDaEntidade")]
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

        if (RecusaDaListagem(entidadeTipo, page) is { } recusa)
        {
            return recusa;
        }

        ListarAtosDaEntidadeResult resultado = await _queryBus
            .Send(new ListarAtosDaEntidadeQuery(new EntidadeVinculada(entidadeTipo, entidadeId), page.AfterSortKey, page.AfterId, page.Limit, page.Direction), cancellationToken)
            .ConfigureAwait(false);

        return await this.OkPaginatedOrdenadoAsync(
            ComLinks(resultado.Items), resultado.Anterior, resultado.Proximo, page, EtiquetaDaEntidade(entidadeTipo, entidadeId),
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Obtém um ato pelo Id, com os avisos de numeração recomputados. Retorna
    /// 404 quando inexistente.
    /// </summary>
    [HttpGet("atos/{id:guid}")]
    [EndpointName("ObterAtoNormativo")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "ato-normativo", Versions = [1])]
    [ProducesResponseType(typeof(AtoNormativoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound,
        Description = "O ato não existe (uniplus.publicacoes.ato_normativo.nao_encontrado).")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken cancellationToken)
    {
        Result<AtoNormativoDto> resultado = await _queryBus
            .Send(new ObterAtoNormativoPorIdQuery(id), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess
            ? Ok(resultado.Value! with { Links = _linksBuilder.Build(resultado.Value!) })
            : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Registra um ato publicado — publicação nova ou retificação de outro ato,
    /// quando o corpo traz o par <c>atoRetificadoId</c>/<c>motivoRetificacao</c>
    /// (ADR-0103). Exige a permissão de registro. <c>Idempotency-Key</c>
    /// obrigatório (ADR-0027). A resposta traz o Id, o instante forense de registro
    /// e eventuais avisos de numeração. Uma retificação que quebra a cadeia
    /// linear (o ato-alvo já foi retificado) responde 409. Um vínculo com objeto cujos
    /// atos pertencem ao seu domínio (o processo seletivo), declarado ou herdado do ato
    /// retificado, é recusado com 422: o endpoint não recebe o arquivo, e o ato desse
    /// objeto é publicado pelo domínio dono.
    /// </summary>
    [HttpPost("admin/atos")]
    [EndpointName("RegistrarAtoNormativo")]
    [ExigePermissao<PermissaoPublicacoes>(PermissaoPublicacoes.AtosNormativosRegistrar, ResourceTipo = ResourceTiposPublicacoes.AtoNormativo)]
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

    /// <summary>Dá a cada ato o seu link <c>self</c> (HATEOAS nível 1, ADR-0029).</summary>
    private AtoNormativoDto[] ComLinks(IEnumerable<AtoNormativoDto> atos) =>
        [.. atos.Select(a => a with { Links = _linksBuilder.Build(a) })];

    /// <summary>
    /// A recusa que a fronteira dá a uma consulta dos atos de uma entidade antes de enviá-la, ou
    /// <c>null</c>: primeiro o tipo de entidade, depois a âncora do cursor.
    /// </summary>
    /// <remarks>
    /// A forma do rótulo é conferida, o valor jamais: sem a recusa, um rótulo fora da grafia canônica
    /// (minúsculo, com hífen) devolveria a mesma coleção vazia de uma entidade sem atos, e o erro de
    /// digitação passaria por resultado legítimo. A âncora é o par (instante da divulgação, Id) e o
    /// instante chega como texto: decodificar o wire é da fronteira (ADR-0031); sem a recusa, o cursor
    /// adulterado viraria 500. O código é o que o binder dá ao cursor que não decodifica.
    /// </remarks>
    private IActionResult? RecusaDaListagem(string entidadeTipo, PageRequest page)
    {
        if (!TipoDaEntidadeVinculada.TemFormaCanonica(entidadeTipo))
        {
            return Result.Failure(new DomainError(
                    AtoNormativoErrorCodes.TipoDeEntidadeInvalido,
                    "O tipo da entidade deve ser um rótulo em maiúsculas, com grupos separados por sublinhado."))
                .ToActionResult(_mapper);
        }

        return AncoraEhInstante(page.AfterSortKey)
            ? null
            : Result.Failure(new DomainError(CursorBindingErrorCodes.Invalido, "O cursor informado é inválido."))
                .ToActionResult(_mapper);
    }

    /// <summary>A âncora desta coleção é o instante da divulgação no formato <c>O</c>; sem âncora, é a primeira página.</summary>
    private static bool AncoraEhInstante(string? ancora) =>
        ancora is null
        || DateTimeOffset.TryParseExact(ancora, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _);

    /// <summary>
    /// Etiqueta do cursor da coleção desta entidade. Composta pelo mesmo utilitário que
    /// o binder usa para conferi-la — se cada lado a montasse à sua maneira, nenhum
    /// cursor emitido aqui passaria na leitura.
    /// </summary>
    private static string EtiquetaDaEntidade(string entidadeTipo, Guid entidadeId) =>
        CursorResourceTag.Compose(
            ResourceTagEntidade, [entidadeTipo, entidadeId.ToString()]);
}
