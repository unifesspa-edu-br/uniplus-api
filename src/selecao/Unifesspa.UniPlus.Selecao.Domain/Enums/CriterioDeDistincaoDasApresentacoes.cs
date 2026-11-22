namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>Critério que distingue as apresentações exigidas de um documento.</summary>
public enum CriterioDeDistincaoDasApresentacoes
{
    Nenhuma = 0,

    /// <summary>Unidade = mês ("AAAA-MM") — "últimos N" = as N competências mensais regulares imediatamente ≤ <see cref="ValueObjects.DistincaoDasApresentacoes.DataReferencia"/>.</summary>
    CompetenciaMensal = 1,

    /// <summary>Unidade = ano ("AAAA") — "últimos N" = os N exercícios anuais regulares imediatamente ≤ <see cref="ValueObjects.DistincaoDasApresentacoes.DataReferencia"/>.</summary>
    ExercicioAnual = 2,

    /// <summary>Eventos irregulares, com identificações esperadas opcionais ou apenas contagem de ocorrências diferentes.</summary>
    Ocorrencia = 3,
}
