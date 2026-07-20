namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Catálogo fechado de 3 valores para distinguir cardinalidade de apresentações de uma FOLHA
/// (Story #921) — duas semânticas: calendário derivável (<see cref="CompetenciaMensal"/>/
/// <see cref="ExercicioAnual"/>, "últimos N" deriva da âncora) ou sem calendário
/// (<see cref="Ocorrencia"/>, eventos irregulares — <c>ocorrenciasEsperadas</c> opcional).
/// Ampliar o catálogo exige nova change.
/// </summary>
public enum ChaveDistincao
{
    Nenhuma = 0,

    /// <summary>Unidade = mês ("AAAA-MM") — "últimos N" = as N competências mensais regulares imediatamente ≤ <see cref="Entities.NoExigencia.DataReferencia"/>.</summary>
    CompetenciaMensal = 1,

    /// <summary>Unidade = ano ("AAAA") — "últimos N" = os N exercícios anuais regulares imediatamente ≤ <see cref="Entities.NoExigencia.DataReferencia"/>.</summary>
    ExercicioAnual = 2,

    /// <summary>Eventos irregulares, sem calendário — <see cref="Entities.NoExigencia.OcorrenciasEsperadas"/> opcional (slots concretos congelados, ou distinção pura por N ocorrências diferentes sem a lista).</summary>
    Ocorrencia = 3,
}
