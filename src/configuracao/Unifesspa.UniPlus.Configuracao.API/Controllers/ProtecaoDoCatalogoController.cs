namespace Unifesspa.UniPlus.Configuracao.API.Controllers;

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
        AcessoAoCatalogoDeProtecao acesso = await AcessoAoCatalogoDeProtecao.VerificarAsync(_verificador, cancellationToken).ConfigureAwait(false);

        Result<IReadOnlyList<ItemDaProtecaoDoCatalogoDto>> resultado = await _queryBus
            .Send(new ListarProtecaoDoCatalogoQuery(acesso, situacao), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// O item da fila com o que a proteção de dados precisa ver para decidir, em qualquer situação da aprovação.
    /// O cadastro é <c>tipos-documento</c> ou <c>fatos</c>. É servido pela permissão de aprovar, sem abrir a consulta
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
    /// Envia à proteção de dados, de uma vez, todos os tipos de documento e fatos pendentes dos cadastros que quem
    /// envia pode manter, e avisa por e-mail quem tem a permissão de aprovar. Idempotency-Key obrigatório (ADR-0027).
    /// </summary>
    [HttpPost("admin/protecao-do-catalogo/solicitacoes")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(typeof(SolicitacaoDaProtecaoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Solicitar(CancellationToken cancellationToken)
    {
        // O acesso e os destinatários vêm do módulo Autorização e são lidos antes do handler, que fica num único
        // DbContext para que a trava, a conferência, a gravação e o aviso por e-mail fiquem na mesma transação.
        AcessoAoCatalogoDeProtecao acesso = await AcessoAoCatalogoDeProtecao.VerificarAsync(_verificador, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<string> destinatarios = await _titulares
            .ListarEmailsAsync(PermissaoConfiguracao.ProtecaoDoCatalogoAprovar.Codigo, cancellationToken)
            .ConfigureAwait(false);

        Result<SolicitacaoDaProtecaoDto> resultado = await _commandBus
            .Send(new SolicitarAprovacaoDaProtecaoCommand(acesso, destinatarios), cancellationToken)
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
