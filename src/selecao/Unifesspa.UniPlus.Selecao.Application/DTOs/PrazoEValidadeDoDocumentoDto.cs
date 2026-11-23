namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// A data de referência do prazo de emissão ou da validade. O PUT recebe de volta o <c>codigo</c> do tipo.
/// </summary>
/// <param name="Tipo">O tipo da data de referência.</param>
/// <param name="Data">A data, só quando o tipo é <c>DATA_ESPECIFICA</c>.</param>
/// <param name="FaseId">A fase do cronograma, só quando o tipo é <c>INICIO_DA_FASE</c> ou <c>FIM_DA_FASE</c>.</param>
public sealed record DataDeReferenciaDoDocumentoDto(CodigoComRotulo<TipoDaDataDeReferencia> Tipo, DateOnly? Data, Guid? FaseId);

/// <summary>
/// O prazo de emissão do documento. O PUT recebe de volta o <c>codigo</c> de cada enum; omitir o objeto apaga a escolha.
/// </summary>
/// <param name="Escolha">Com prazo ou sem prazo.</param>
/// <param name="Valor">Quantidade de dias, meses ou anos; só com prazo.</param>
/// <param name="Unidade">Unidade do prazo; só com prazo.</param>
/// <param name="Referencia">A data de referência da contagem; só com prazo.</param>
/// <param name="EfeitoForaDoPrazo">O efeito na análise do documento emitido fora do prazo; só com prazo.</param>
public sealed record PrazoDeEmissaoDto(
    CodigoComRotulo<EscolhaDoPrazoDeEmissao> Escolha,
    int? Valor,
    CodigoComRotulo<UnidadeDoPrazoDeEmissao>? Unidade,
    DataDeReferenciaDoDocumentoDto? Referencia,
    CodigoComRotulo<EfeitoNaAnaliseDoDocumento>? EfeitoForaDoPrazo);

/// <summary>
/// A validade do documento. O PUT recebe de volta o <c>codigo</c> de cada enum; omitir o objeto apaga a escolha.
/// </summary>
/// <param name="Escolha">Exige ou não exige documento válido.</param>
/// <param name="Referencia">A data de referência da conferência; só quando exige documento válido.</param>
/// <param name="EfeitoDoVencimento">O efeito na análise do documento vencido; só quando exige documento válido.</param>
public sealed record ValidadeDoDocumentoDto(
    CodigoComRotulo<EscolhaDaValidade> Escolha,
    DataDeReferenciaDoDocumentoDto? Referencia,
    CodigoComRotulo<EfeitoNaAnaliseDoDocumento>? EfeitoDoVencimento);
