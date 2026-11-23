namespace Unifesspa.UniPlus.Configuracao.Application.DTOs;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// A data de referência do prazo de emissão ou da validade do documento do modelo. O PUT recebe de volta o
/// <c>codigo</c> do tipo.
/// </summary>
/// <param name="Tipo">O tipo da data de referência.</param>
/// <param name="FaseCodigo">O código da fase, só quando o tipo é <c>INICIO_DA_FASE</c> ou <c>FIM_DA_FASE</c>.</param>
/// <param name="Data">A data, só quando o tipo é <c>DATA_ESPECIFICA</c>.</param>
public sealed record DataDeReferenciaDoDocumentoDoModeloDto(CodigoComRotulo<TipoDaDataDeReferencia> Tipo, string? FaseCodigo, DateOnly? Data);

/// <summary>
/// O prazo de emissão do documento do modelo. O PUT recebe de volta o <c>codigo</c> de cada enum.
/// </summary>
/// <param name="Escolha">Com prazo ou sem prazo.</param>
/// <param name="Valor">Quantidade de dias, meses ou anos.</param>
/// <param name="Unidade">Unidade do prazo.</param>
/// <param name="Referencia">A data de referência da contagem.</param>
/// <param name="EfeitoForaDoPrazo">O efeito na análise do documento emitido fora do prazo.</param>
public sealed record PrazoDeEmissaoDoModeloDto(
    CodigoComRotulo<EscolhaDoPrazoDeEmissao> Escolha,
    int? Valor,
    CodigoComRotulo<UnidadeDoPrazoDeEmissao>? Unidade,
    DataDeReferenciaDoDocumentoDoModeloDto? Referencia,
    CodigoComRotulo<EfeitoNaAnaliseDoDocumento>? EfeitoForaDoPrazo);

/// <summary>
/// A validade do documento do modelo. O PUT recebe de volta o <c>codigo</c> de cada enum.
/// </summary>
/// <param name="Escolha">Exige ou não exige documento válido.</param>
/// <param name="Referencia">A data de referência da conferência.</param>
/// <param name="EfeitoDoVencimento">O efeito na análise do documento vencido.</param>
public sealed record ValidadeDoDocumentoDoModeloDto(
    CodigoComRotulo<EscolhaDaValidade> Escolha,
    DataDeReferenciaDoDocumentoDoModeloDto? Referencia,
    CodigoComRotulo<EfeitoNaAnaliseDoDocumento>? EfeitoDoVencimento);
