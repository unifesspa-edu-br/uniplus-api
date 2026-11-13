namespace Unifesspa.UniPlus.Configuracao.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.API.Contracts.Requests;
using Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Permissoes;
using Unifesspa.UniPlus.Configuracao.Application.Queries.ProtecaoDoCatalogo;
using Unifesspa.UniPlus.Infrastructure.Core.Authorization;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O circuito de aprovação da proteção de dados dos tipos de documento e dos fatos do candidato (ADR-0153): o que
/// aguarda a aprovação, a solicitação que o administrador do cadastro envia, a manifestação técnica do Encarregado de
/// dados e a decisão do Representante do controlador (ADR-0163). O aceite da recomendação fica no cadastro de cada
/// item, que é de quem o mantém.
/// </summary>
[ApiController]
[Authorize]
public sealed class ProtecaoDoCatalogoController : ControllerBase
{
    private const string DecisoesResourceTag = "protecao-do-catalogo-decisoes";

    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;

    public ProtecaoDoCatalogoController(
        ICommandBus commandBus,
        IQueryBus queryBus,
        IDomainErrorMapper mapper)
    {
        _commandBus = commandBus;
        _queryBus = queryBus;
        _mapper = mapper;
    }

    /// <summary>
    /// Lista os tipos de documento e fatos cuja proteção ainda não foi aprovada, com a situação de cada um. Quem
    /// aprova ou consulta a proteção vê os dois cadastros; quem só mantém um cadastro vê o dele. A leitura de quem aprova
    /// ou consulta é registrada antes da resposta (sem o registro, 503). O acesso é decidido pela permissão de
    /// cada cadastro, e não por um único atributo, porque a lista alcança os dois.
    /// </summary>
    [HttpGet("admin/protecao-do-catalogo")]
    [VendorMediaType(Resource = "protecao-do-catalogo", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<ItemDaProtecaoDoCatalogoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Listar([FromQuery] string? situacao, CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<ItemDaProtecaoDoCatalogoDto>> resultado = await _queryBus
            .Send(new ListarProtecaoDoCatalogoQuery(situacao), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// As decisões que o Representante do controlador registrou — aprovação, ajuste recomendado e reprovação —,
    /// paginadas por cursor opaco bidirecional (ADR-0026 + ADR-0094), das mais recentes para as mais antigas. Quem
    /// aprova ou consulta a proteção de dados do catálogo de tipos de documento e de fatos do candidato vê todas, e a
    /// leitura é registrada antes da resposta (sem o registro, 503); <c>minhas=true</c> restringe às de quem consulta.
    /// Os filtros <c>ato</c> (repetível), <c>cadastro</c>, <c>de</c> e <c>ate</c> (datas, inclusive) e <c>busca</c>
    /// (nome ou código do item, sem distinguir maiúsculas nem acentos) se combinam.
    /// </summary>
    [HttpGet("admin/protecao-do-catalogo/decisoes")]
    [ExigePermissao<PermissaoConfiguracao>(
        PermissaoConfiguracao.ProtecaoDoCatalogoAprovar,
        ResourceTipo = ResourceTiposConfiguracao.ProtecaoDoCatalogo,
        OuPermissao = PermissaoConfiguracao.ProtecaoDoCatalogoConsultar)]
    [VendorMediaType(Resource = "protecao-do-catalogo-decisao", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<DecisaoDaProtecaoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ListarDecisoes(
        [FromCursor(DecisoesResourceTag, RequireSortKey = true)] PageRequest page,
        [FromQuery] FiltroDasDecisoesDaProtecaoRequest filtro,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(filtro);

        Result<ListarDecisoesDaProtecaoResult> resultado = await _queryBus.Send(filtro.ParaConsulta(page), cancellationToken).ConfigureAwait(false);

        return resultado.IsFailure
            ? resultado.ToActionResult(_mapper)
            : await this.OkPaginatedOrdenadoAsync(
                resultado.Value!.Items, resultado.Value.Anterior, resultado.Value.Proximo, page, DecisoesResourceTag,
                cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// O item da fila com o que o Representante do controlador precisa ver para decidir, em qualquer situação da
    /// aprovação, e todos os atos registrados do item em ordem cronológica. O cadastro é <c>tipos-documento</c> ou
    /// <c>fatos</c>. É servido pela permissão de aprovar ou pela de consultar a proteção de dados do catálogo de tipos
    /// de documento e de fatos do candidato, sem abrir a consulta aos cadastros. A leitura é registrada antes da
    /// resposta (sem o registro, 503).
    /// </summary>
    [HttpGet("admin/protecao-do-catalogo/{cadastro:regex(^(tipos-documento|fatos)$)}/{id:guid}")]
    [ExigePermissao<PermissaoConfiguracao>(
        PermissaoConfiguracao.ProtecaoDoCatalogoAprovar,
        ResourceTipo = ResourceTiposConfiguracao.ProtecaoDoCatalogo,
        OuPermissao = PermissaoConfiguracao.ProtecaoDoCatalogoConsultar)]
    [VendorMediaType(Resource = "protecao-do-catalogo-detalhe", Versions = [1])]
    [ProducesResponseType(typeof(DetalheDaProtecaoDoCatalogoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ObterDetalhe(string cadastro, Guid id, CancellationToken cancellationToken)
    {
        DetalheDaProtecaoDoCatalogoDto? detalhe = await _queryBus
            .Send(new ObterDetalheDaProtecaoDoCatalogoQuery(CadastroDaRota(cadastro), id), cancellationToken)
            .ConfigureAwait(false);

        return detalhe is null ? NotFound() : Ok(detalhe);
    }

    /// <summary>
    /// Todas as manifestações técnicas do item, de todos os ciclos, com a situação de cada uma: vigente, substituída ou
    /// encerrada. O cadastro é <c>tipos-documento</c> ou <c>fatos</c>. É servido pela permissão de aprovar ou pela de
    /// consultar a proteção de dados do catálogo de tipos de documento e de fatos do candidato, e a leitura é registrada
    /// antes da resposta (sem o registro, 503).
    /// </summary>
    [HttpGet("admin/protecao-do-catalogo/{cadastro:regex(^(tipos-documento|fatos)$)}/{id:guid}/manifestacoes")]
    [ExigePermissao<PermissaoConfiguracao>(
        PermissaoConfiguracao.ProtecaoDoCatalogoAprovar,
        ResourceTipo = ResourceTiposConfiguracao.ProtecaoDoCatalogo,
        OuPermissao = PermissaoConfiguracao.ProtecaoDoCatalogoConsultar)]
    [VendorMediaType(Resource = "protecao-do-catalogo-manifestacao", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<ManifestacaoDaProtecaoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ListarManifestacoes(string cadastro, Guid id, CancellationToken cancellationToken)
    {
        IReadOnlyList<ManifestacaoDaProtecaoDto>? manifestacoes = await _queryBus
            .Send(new ListarManifestacoesDoItemDaProtecaoQuery(CadastroDaRota(cadastro), id), cancellationToken)
            .ConfigureAwait(false);

        return manifestacoes is null ? NotFound() : Ok(manifestacoes);
    }

    /// <summary>
    /// Envia à proteção de dados, de uma vez, os tipos de documento e fatos pendentes que quem envia escolheu —
    /// cada item com o cadastro (<c>TIPO_DOCUMENTO</c> ou <c>FATO</c>) e o identificador —, desde que mantenha o
    /// cadastro, e avisa por e-mail quem tem a permissão de aprovar. Idempotency-Key obrigatório (ADR-0027).
    /// </summary>
    [HttpPost("admin/protecao-do-catalogo/solicitacoes")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(typeof(SolicitacaoDaProtecaoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Solicitar([FromBody] SolicitarAprovacaoDaProtecaoRequest? request, CancellationToken cancellationToken)
    {
        Result<SolicitacaoDaProtecaoDto> resultado = await _commandBus
            .Send(new SolicitarAprovacaoDaProtecaoCommand(request?.ParaItens() ?? []), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// A manifestação técnica sobre a proteção de um item que está numa solicitação, com o ciclo de aprovação lido no
    /// item. A nova manifestação da mesma pessoa no ciclo substitui a vigente sem apagá-la; a manifestação não decide.
    /// Exige a permissão de se manifestar sobre a proteção de dados do catálogo de tipos de documento e de fatos do
    /// candidato. Idempotency-Key obrigatório (ADR-0027).
    /// </summary>
    [HttpPost("admin/protecao-do-catalogo/manifestacoes")]
    [ExigePermissao<PermissaoConfiguracao>(PermissaoConfiguracao.ProtecaoDoCatalogoManifestar, ResourceTipo = ResourceTiposConfiguracao.ProtecaoDoCatalogo)]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Manifestar([FromBody] RegistrarManifestacaoDaProtecaoRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result resultado = await _commandBus.Send(request.ParaComando(), cancellationToken).ConfigureAwait(false);

        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// O Representante do controlador aprova ou nega um item que está numa solicitação; a negativa exige a
    /// classificação recomendada. Diante de manifestação técnica desfavorável vigente, aprovar ou recomendar ajuste
    /// exige a justificativa. Exige a permissão de aprovar a proteção de dados do catálogo de tipos de documento e de
    /// fatos do candidato. Idempotency-Key obrigatório (ADR-0027).
    /// </summary>
    [HttpPost("admin/protecao-do-catalogo")]
    [ExigePermissao<PermissaoConfiguracao>(PermissaoConfiguracao.ProtecaoDoCatalogoAprovar, ResourceTipo = ResourceTiposConfiguracao.ProtecaoDoCatalogo)]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Decidir([FromBody] DecidirProtecaoDoCatalogoRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result resultado = await _commandBus.Send(request.ParaComando(), cancellationToken).ConfigureAwait(false);

        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }

    private static CadastroDaProtecaoDoCatalogo CadastroDaRota(string cadastro) =>
        string.Equals(cadastro, "fatos", StringComparison.OrdinalIgnoreCase) ? CadastroDaProtecaoDoCatalogo.Fato : CadastroDaProtecaoDoCatalogo.TipoDocumento;
}
