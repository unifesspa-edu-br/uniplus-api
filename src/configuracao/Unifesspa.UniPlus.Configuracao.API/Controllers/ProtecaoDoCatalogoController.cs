namespace Unifesspa.UniPlus.Configuracao.API.Controllers;

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Authorization.Abstractions;
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
/// O circuito de aprovação da proteção de dados dos tipos de documento e dos fatos do catálogo (ADR-0153): o que
/// aguarda a aprovação, a solicitação que o administrador envia e a decisão da proteção de dados. O aceite da
/// recomendação fica no cadastro de cada item, que é de quem o mantém.
/// </summary>
[ApiController]
[Authorize]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public.")]
public sealed class ProtecaoDoCatalogoController : ControllerBase
{
    private const string DecisoesResourceTag = "protecao-do-catalogo-decisoes";

    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;
    private readonly IVerificadorDeAcesso _verificador;
    private readonly ITitularesDaPermissao _titulares;

    public ProtecaoDoCatalogoController(
        ICommandBus commandBus,
        IQueryBus queryBus,
        IDomainErrorMapper mapper,
        IVerificadorDeAcesso verificador,
        ITitularesDaPermissao titulares)
    {
        _commandBus = commandBus;
        _queryBus = queryBus;
        _mapper = mapper;
        _verificador = verificador;
        _titulares = titulares;
    }

    /// <summary>
    /// Lista os tipos de documento e fatos cuja proteção ainda não foi aprovada, com a situação de cada um. Quem
    /// aprova vê os dois cadastros; quem só mantém um cadastro vê o dele. O acesso é decidido pela permissão de
    /// cada cadastro, e não por um único atributo, porque a lista alcança os dois.
    /// </summary>
    [HttpGet("admin/protecao-do-catalogo")]
    [VendorMediaType(Resource = "protecao-do-catalogo", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<ItemDaProtecaoDoCatalogoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Listar([FromQuery] string? situacao, CancellationToken cancellationToken)
    {
        Result<AcessoAoCatalogoDeProtecao> acesso = await AcessoAoCatalogoDeProtecao.VerificarAsync(_verificador, cancellationToken).ConfigureAwait(false);
        if (acesso.IsFailure)
        {
            return acesso.ToActionResult(_mapper);
        }

        Result<IReadOnlyList<ItemDaProtecaoDoCatalogoDto>> resultado = await _queryBus
            .Send(new ListarProtecaoDoCatalogoQuery(acesso.Value, situacao), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// As decisões que a proteção de dados registrou — aprovação, ajuste recomendado e reprovação —, paginadas por
    /// cursor opaco bidirecional (ADR-0026 + ADR-0094), das mais recentes para as mais antigas. Quem aprova vê todas;
    /// <c>minhas=true</c> restringe às de quem consulta. Os filtros <c>ato</c> (repetível), <c>cadastro</c>,
    /// <c>de</c> e <c>ate</c> (datas, inclusive) e <c>busca</c> (nome ou código do item, sem distinguir maiúsculas
    /// nem acentos) se combinam.
    /// </summary>
    [HttpGet("admin/protecao-do-catalogo/decisoes")]
    [ExigePermissao<PermissaoConfiguracao>(PermissaoConfiguracao.ProtecaoDoCatalogoAprovar, ResourceTipo = ResourceTiposConfiguracao.ProtecaoDoCatalogo)]
    [VendorMediaType(Resource = "protecao-do-catalogo-decisao", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<DecisaoDaProtecaoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ListarDecisoes(
        [FromCursor(DecisoesResourceTag, RequireSortKey = true)] PageRequest page,
        [FromQuery(Name = "ato")]
        [Description("Decisão: APROVACAO, AJUSTE_RECOMENDADO ou REPROVACAO. Repetível; sem o parâmetro, as três.")]
        string[]? ato,
        [FromQuery(Name = "cadastro")]
        [Description("Cadastro do item: TIPO_DOCUMENTO ou FATO.")]
        string? cadastro,
        [FromQuery(Name = "de")]
        [Description("Data inicial da decisão, inclusive (AAAA-MM-DD), no fuso de Belém.")]
        DateOnly? de,
        [FromQuery(Name = "ate")]
        [Description("Data final da decisão, inclusive (AAAA-MM-DD), no fuso de Belém.")]
        DateOnly? ate,
        [FromQuery(Name = "busca")]
        [Description("Trecho do nome ou do código do item. Insensível a caixa e a acentuação. Até 200 caracteres.")]
        string? busca,
        [FromQuery(Name = "minhas")]
        [Description("Quando verdadeiro, só as decisões de quem consulta.")]
        bool minhas = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);

        Result<ListarDecisoesDaProtecaoResult> resultado = await _queryBus
            .Send(
                new ListarDecisoesDaProtecaoQuery(
                    ato ?? [], cadastro, de, ate, busca, minhas, page.AfterSortKey, page.AfterId, page.Limit, page.Direction),
                cancellationToken)
            .ConfigureAwait(false);

        if (resultado.IsFailure)
        {
            return resultado.ToActionResult(_mapper);
        }

        return await this.OkPaginatedOrdenadoAsync(
            resultado.Value!.Items, resultado.Value.Anterior, resultado.Value.Proximo, page, DecisoesResourceTag,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// O item da fila com o que a proteção de dados precisa ver para decidir, em qualquer situação da aprovação, e
    /// todos os atos registrados do item em ordem cronológica. O cadastro é <c>tipos-documento</c> ou <c>fatos</c>. É servido pela permissão de aprovar, sem abrir a consulta
    /// aos cadastros.
    /// </summary>
    [HttpGet("admin/protecao-do-catalogo/{cadastro:regex(^(tipos-documento|fatos)$)}/{id:guid}")]
    [ExigePermissao<PermissaoConfiguracao>(PermissaoConfiguracao.ProtecaoDoCatalogoAprovar, ResourceTipo = ResourceTiposConfiguracao.ProtecaoDoCatalogo)]
    [VendorMediaType(Resource = "protecao-do-catalogo-detalhe", Versions = [1])]
    [ProducesResponseType(typeof(DetalheDaProtecaoDoCatalogoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ObterDetalhe(string cadastro, Guid id, CancellationToken cancellationToken)
    {
        CadastroDaProtecaoDoCatalogo origem = string.Equals(cadastro, "fatos", StringComparison.OrdinalIgnoreCase) ? CadastroDaProtecaoDoCatalogo.Fato : CadastroDaProtecaoDoCatalogo.TipoDocumento;

        DetalheDaProtecaoDoCatalogoDto? detalhe = await _queryBus
            .Send(new ObterDetalheDaProtecaoDoCatalogoQuery(origem, id), cancellationToken)
            .ConfigureAwait(false);

        return detalhe is null ? NotFound() : Ok(detalhe);
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
        // O acesso e os destinatários vêm do módulo Autorização e são lidos antes do handler, que fica num único
        // DbContext para que a trava, a conferência, a gravação e o aviso por e-mail fiquem na mesma transação.
        Result<AcessoAoCatalogoDeProtecao> acesso = await AcessoAoCatalogoDeProtecao.VerificarAsync(_verificador, cancellationToken).ConfigureAwait(false);
        if (acesso.IsFailure)
        {
            return acesso.ToActionResult(_mapper);
        }

        IReadOnlyList<string> destinatarios = await _titulares
            .ListarEmailsAsync(PermissaoConfiguracao.ProtecaoDoCatalogoAprovar.Codigo, cancellationToken)
            .ConfigureAwait(false);

        Result<SolicitacaoDaProtecaoDto> resultado = await _commandBus
            .Send(new SolicitarAprovacaoDaProtecaoCommand(acesso.Value, destinatarios, request?.ParaItens() ?? []), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// A proteção de dados aprova ou nega um item que está numa solicitação; a negativa exige a classificação
    /// recomendada. Exige a permissão de aprovar a proteção de dados do catálogo. Idempotency-Key obrigatório (ADR-0027).
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
}
