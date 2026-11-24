namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Como a nota de uma etapa é obtida. É declarada pela etapa, e não pelo tipo de etapa: dois
/// editais podem avaliar a mesma espécie de etapa de formas diferentes.
/// </summary>
/// <remarks>
/// A dupla correção não é uma terceira forma. Ela qualifica a avaliação humana e é declarada na
/// regra de apuração da avaliação, de modo que o conjunto aqui continua fechado em dois valores.
/// A ausência de forma é <see langword="null"/> na etapa, e é estado legítimo em rascunho.
/// </remarks>
public enum FormaDeAvaliacao
{
    [CodigoDeContrato(
        "OBJETIVA",
        "Avaliação objetiva",
        Descricao = "Gabarito ou leitora óptica: a nota sai sem avaliador.")]
    Objetiva = 1,

    [CodigoDeContrato(
        "HUMANA",
        "Avaliação humana",
        Descricao = "Pessoas da banca da etapa lançam as notas.")]
    Humana = 2,
}
