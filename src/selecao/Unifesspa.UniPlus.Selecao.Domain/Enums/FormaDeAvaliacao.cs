namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Como a nota de uma etapa é obtida. É declarada pela etapa, e não pelo tipo de etapa: dois
/// editais podem avaliar a mesma espécie de etapa de formas diferentes.
/// </summary>
/// <remarks>
/// A dupla correção não é uma terceira forma. Ela qualifica a avaliação humana e é declarada na
/// regra de consolidação, de modo que o conjunto aqui continua fechado em dois valores. Sem
/// membro sentinela: a ausência de forma é <see langword="null"/> na etapa, e é estado legítimo
/// em rascunho.
/// </remarks>
public enum FormaDeAvaliacao
{
    /// <summary>Gabarito ou leitora óptica: a nota sai sem avaliador.</summary>
    Objetiva = 1,

    /// <summary>Pessoas da banca da etapa lançam as notas.</summary>
    Humana = 2,
}
