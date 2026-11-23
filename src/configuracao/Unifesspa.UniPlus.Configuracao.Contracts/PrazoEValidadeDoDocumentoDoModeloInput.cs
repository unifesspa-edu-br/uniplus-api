namespace Unifesspa.UniPlus.Configuracao.Contracts;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// A data a partir da qual se conta o prazo de emissão ou se confere a validade do documento do modelo.
/// </summary>
/// <param name="Tipo">Código do tipo da data de referência.</param>
/// <param name="Data">A data, só quando o tipo é <c>DATA_ESPECIFICA</c>.</param>
/// <param name="FaseCodigo">O código da fase, só quando o tipo é <c>INICIO_DA_FASE</c> ou <c>FIM_DA_FASE</c>; o processo o troca pela fase do cronograma.</param>
public sealed record DataDeReferenciaDoDocumentoDoModeloInput(
    [AceitaCodigosDe<TipoDaDataDeReferencia>] string? Tipo, DateOnly? Data, string? FaseCodigo);

/// <summary>
/// O prazo de emissão do documento do modelo. Que o valor, a unidade, a referência e o efeito combinem com a
/// escolha quem confere é o processo, ao receber a cópia.
/// </summary>
/// <param name="Escolha">Código da escolha: com prazo ou sem prazo.</param>
/// <param name="Valor">Quantidade de dias, meses ou anos.</param>
/// <param name="Unidade">Código da unidade do prazo.</param>
/// <param name="Referencia">A data de referência da contagem.</param>
/// <param name="EfeitoForaDoPrazo">Código do efeito na análise do documento emitido fora do prazo.</param>
public sealed record PrazoDeEmissaoDoModeloInput(
    [AceitaCodigosDe<EscolhaDoPrazoDeEmissao>] string? Escolha,
    int? Valor,
    [AceitaCodigosDe<UnidadeDoPrazoDeEmissao>] string? Unidade,
    DataDeReferenciaDoDocumentoDoModeloInput? Referencia,
    [AceitaCodigosDe<EfeitoNaAnaliseDoDocumento>] string? EfeitoForaDoPrazo);

/// <summary>
/// A validade do documento do modelo. Que a referência e o efeito combinem com a escolha quem confere é o
/// processo, ao receber a cópia.
/// </summary>
/// <param name="Escolha">Código da escolha: exige ou não exige documento válido.</param>
/// <param name="Referencia">A data de referência da conferência.</param>
/// <param name="EfeitoDoVencimento">Código do efeito na análise do documento vencido.</param>
public sealed record ValidadeDoDocumentoDoModeloInput(
    [AceitaCodigosDe<EscolhaDaValidade>] string? Escolha,
    DataDeReferenciaDoDocumentoDoModeloInput? Referencia,
    [AceitaCodigosDe<EfeitoNaAnaliseDoDocumento>] string? EfeitoDoVencimento);
