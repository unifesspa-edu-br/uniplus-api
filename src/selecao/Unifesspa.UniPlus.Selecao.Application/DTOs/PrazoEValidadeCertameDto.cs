namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// De que data o prazo de emissão ou a validade de um documento são contados, como o edital congelou.
/// </summary>
/// <param name="Tipo">O tipo da data de referência.</param>
/// <param name="Data">
/// A data já resolvida no cronograma congelado, no fuso do processo. Nula quando a referência é a data da apresentação
/// do documento, que só existe quando o candidato o apresenta.
/// </param>
/// <param name="FaseId">A fase do cronograma, só quando o tipo é início ou fim de fase.</param>
public sealed record ReferenciaDoDocumentoCertameDto(CodigoComRotulo<TipoDaDataDeReferencia> Tipo, DateOnly? Data, Guid? FaseId);

/// <summary>O prazo de emissão que o edital exige do documento: quanto tempo antes da referência ele pode ter sido emitido.</summary>
/// <param name="Escolha">Sempre com prazo: sem prazo, o prazo de emissão do documento é nulo.</param>
/// <param name="Valor">Quantidade de dias, meses ou anos.</param>
/// <param name="Unidade">Dias, meses ou anos.</param>
/// <param name="Referencia">A data de onde o prazo é contado.</param>
/// <param name="Efeito">O que acontece na análise com o documento emitido fora do prazo.</param>
public sealed record PrazoDeEmissaoCertameDto(
    CodigoComRotulo<EscolhaDoPrazoDeEmissao> Escolha,
    int Valor,
    CodigoComRotulo<UnidadeDoPrazoDeEmissao> Unidade,
    ReferenciaDoDocumentoCertameDto Referencia,
    CodigoComRotulo<EfeitoNaAnaliseDoDocumento> Efeito);

/// <summary>A exigência de que o documento esteja válido na data de referência.</summary>
/// <param name="Escolha">Sempre exige documento válido: sem exigência, a validade do documento é nula.</param>
/// <param name="Referencia">A data em que o documento precisa estar válido.</param>
/// <param name="Efeito">O que acontece na análise com o documento vencido.</param>
public sealed record ValidadeDoDocumentoCertameDto(
    CodigoComRotulo<EscolhaDaValidade> Escolha,
    ReferenciaDoDocumentoCertameDto Referencia,
    CodigoComRotulo<EfeitoNaAnaliseDoDocumento> Efeito);
