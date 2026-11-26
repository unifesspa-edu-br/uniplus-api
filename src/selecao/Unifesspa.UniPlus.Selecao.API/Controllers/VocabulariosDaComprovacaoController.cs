namespace Unifesspa.UniPlus.Selecao.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.Queries.Vocabularios;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>Vocabulário das regras de comprovação documental.</summary>
[ApiController]
[Tags("Vocabularios")]
public sealed class VocabulariosDaComprovacaoController(IQueryBus queryBus) : ControllerBase
{
    /// <summary>Lista as formas de declarar uma regra de comprovação.</summary>
    [HttpGet("vocabularios/formas-comprovacao")]
    [EndpointName("ListarFormasDeComprovacao")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "forma-de-comprovacao", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<CodigoComRotulo<FormaDeComprovacao>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public Task<IActionResult> ListarFormasDeComprovacao(CancellationToken cancellationToken) =>
        Listar(new ListarFormasDeComprovacaoQuery(), cancellationToken);

    /// <summary>Lista as escolhas do prazo de emissão.</summary>
    [HttpGet("vocabularios/escolhas-prazo-emissao")]
    [EndpointName("ListarEscolhasDoPrazoDeEmissao")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "escolha-prazo-emissao", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<CodigoComRotulo<EscolhaDoPrazoDeEmissao>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public Task<IActionResult> ListarEscolhasDoPrazoDeEmissao(CancellationToken cancellationToken) =>
        Listar(new ListarEscolhasDoPrazoDeEmissaoQuery(), cancellationToken);

    /// <summary>Lista as escolhas da validade.</summary>
    [HttpGet("vocabularios/escolhas-validade")]
    [EndpointName("ListarEscolhasDaValidade")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "escolha-validade", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<CodigoComRotulo<EscolhaDaValidade>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public Task<IActionResult> ListarEscolhasDaValidade(CancellationToken cancellationToken) =>
        Listar(new ListarEscolhasDaValidadeQuery(), cancellationToken);

    /// <summary>Lista as unidades do prazo de emissão.</summary>
    [HttpGet("vocabularios/unidades-prazo-emissao")]
    [EndpointName("ListarUnidadesDoPrazoDeEmissao")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "unidade-prazo-emissao", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<CodigoComRotulo<UnidadeDoPrazoDeEmissao>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public Task<IActionResult> ListarUnidadesDoPrazoDeEmissao(CancellationToken cancellationToken) =>
        Listar(new ListarUnidadesDoPrazoDeEmissaoQuery(), cancellationToken);

    /// <summary>Lista os tipos da data de referência.</summary>
    [HttpGet("vocabularios/tipos-data-referencia")]
    [EndpointName("ListarTiposDaDataDeReferencia")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "tipo-data-referencia", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<CodigoComRotulo<TipoDaDataDeReferencia>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public Task<IActionResult> ListarTiposDaDataDeReferencia(CancellationToken cancellationToken) =>
        Listar(new ListarTiposDaDataDeReferenciaQuery(), cancellationToken);

    /// <summary>Lista os efeitos na análise do documento.</summary>
    [HttpGet("vocabularios/efeitos-analise-documento")]
    [EndpointName("ListarEfeitosNaAnaliseDoDocumento")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "efeito-analise-documento", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<CodigoComRotulo<EfeitoNaAnaliseDoDocumento>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public Task<IActionResult> ListarEfeitosNaAnaliseDoDocumento(CancellationToken cancellationToken) =>
        Listar(new ListarEfeitosNaAnaliseDoDocumentoQuery(), cancellationToken);

    private async Task<IActionResult> Listar<TEnum>(
        IQuery<IReadOnlyList<CodigoComRotulo<TEnum>>> consulta, CancellationToken cancellationToken)
        where TEnum : struct, Enum
    {
        IReadOnlyList<CodigoComRotulo<TEnum>> opcoes = await queryBus.Send(consulta, cancellationToken).ConfigureAwait(false);
        return Ok(opcoes);
    }

    /// <summary>Lista os formatos de arquivo permitidos para a comprovação.</summary>
    [HttpGet("vocabularios/formatos-permitidos")]
    [EndpointName("ListarFormatosPermitidosDoDocumento")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "formato-permitido", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<CodigoComRotulo<FormatoPermitido>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ListarFormatosPermitidos(CancellationToken cancellationToken)
    {
        IReadOnlyList<CodigoComRotulo<FormatoPermitido>> opcoes = await queryBus
            .Send(new ListarFormatosPermitidosQuery(), cancellationToken).ConfigureAwait(false);
        return Ok(opcoes);
    }

    /// <summary>Lista os critérios que distinguem uma apresentação de outra do mesmo documento.</summary>
    [HttpGet("vocabularios/criterios-distincao-apresentacoes")]
    [EndpointName("ListarCriteriosDeDistincaoDasApresentacoes")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "criterio-distincao-apresentacoes", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<CodigoComRotulo<CriterioDeDistincaoDasApresentacoes>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ListarCriteriosDeDistincaoDasApresentacoes(CancellationToken cancellationToken)
    {
        IReadOnlyList<CodigoComRotulo<CriterioDeDistincaoDasApresentacoes>> opcoes = await queryBus
            .Send(new ListarCriteriosDeDistincaoDasApresentacoesQuery(), cancellationToken).ConfigureAwait(false);
        return Ok(opcoes);
    }

    /// <summary>Lista as consequências do indeferimento de um documento exigido.</summary>
    [HttpGet("vocabularios/consequencias-descumprimento")]
    [EndpointName("ListarConsequenciasDoDescumprimento")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "consequencia-indeferimento", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<CodigoComRotulo<ConsequenciaDoDescumprimento>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ListarConsequenciasDoDescumprimento(CancellationToken cancellationToken)
    {
        IReadOnlyList<CodigoComRotulo<ConsequenciaDoDescumprimento>> opcoes = await queryBus
            .Send(new ListarConsequenciasDoDescumprimentoQuery(), cancellationToken).ConfigureAwait(false);
        return Ok(opcoes);
    }
}
