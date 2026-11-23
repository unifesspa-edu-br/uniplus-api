namespace Unifesspa.UniPlus.Selecao.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
    [AllowAnonymous]
    [VendorMediaType(Resource = "forma-de-comprovacao", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<CodigoComRotulo<FormaDeComprovacao>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public Task<IActionResult> ListarFormasDeComprovacao(CancellationToken cancellationToken) =>
        Listar(new ListarFormasDeComprovacaoQuery(), cancellationToken);

    /// <summary>Lista as escolhas do prazo de emissão.</summary>
    [HttpGet("vocabularios/escolhas-prazo-emissao")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "escolha-prazo-emissao", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<CodigoComRotulo<EscolhaDoPrazoDeEmissao>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public Task<IActionResult> ListarEscolhasDoPrazoDeEmissao(CancellationToken cancellationToken) =>
        Listar(new ListarEscolhasDoPrazoDeEmissaoQuery(), cancellationToken);

    /// <summary>Lista as escolhas da validade.</summary>
    [HttpGet("vocabularios/escolhas-validade")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "escolha-validade", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<CodigoComRotulo<EscolhaDaValidade>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public Task<IActionResult> ListarEscolhasDaValidade(CancellationToken cancellationToken) =>
        Listar(new ListarEscolhasDaValidadeQuery(), cancellationToken);

    /// <summary>Lista as unidades do prazo de emissão.</summary>
    [HttpGet("vocabularios/unidades-prazo-emissao")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "unidade-prazo-emissao", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<CodigoComRotulo<UnidadeDoPrazoDeEmissao>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public Task<IActionResult> ListarUnidadesDoPrazoDeEmissao(CancellationToken cancellationToken) =>
        Listar(new ListarUnidadesDoPrazoDeEmissaoQuery(), cancellationToken);

    /// <summary>Lista os tipos da data de referência.</summary>
    [HttpGet("vocabularios/tipos-data-referencia")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "tipo-data-referencia", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<CodigoComRotulo<TipoDaDataDeReferencia>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public Task<IActionResult> ListarTiposDaDataDeReferencia(CancellationToken cancellationToken) =>
        Listar(new ListarTiposDaDataDeReferenciaQuery(), cancellationToken);

    /// <summary>Lista os efeitos na análise do documento.</summary>
    [HttpGet("vocabularios/efeitos-analise-documento")]
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
}
