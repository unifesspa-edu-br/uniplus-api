namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>Códigos do critério de distinção das apresentações.</summary>
public static class CriterioDeDistincaoDasApresentacoesCodigo
{
    public const string CompetenciaMensal = "COMPETENCIA_MENSAL";
    public const string ExercicioAnual = "EXERCICIO_ANUAL";
    public const string Ocorrencia = "OCORRENCIA";

    public static string ToCodigo(this CriterioDeDistincaoDasApresentacoes criterio) => criterio switch
    {
        CriterioDeDistincaoDasApresentacoes.CompetenciaMensal => CompetenciaMensal,
        CriterioDeDistincaoDasApresentacoes.ExercicioAnual => ExercicioAnual,
        CriterioDeDistincaoDasApresentacoes.Ocorrencia => Ocorrencia,
        CriterioDeDistincaoDasApresentacoes.Nenhuma => throw new ArgumentOutOfRangeException(
            nameof(criterio), criterio, "CriterioDeDistincaoDasApresentacoes.Nenhuma é sentinela e não tem código canônico."),
        _ => throw new ArgumentOutOfRangeException(nameof(criterio), criterio, "CriterioDeDistincaoDasApresentacoes desconhecido."),
    };

    public static CriterioDeDistincaoDasApresentacoes FromCodigo(string? codigo) => codigo switch
    {
        CompetenciaMensal => CriterioDeDistincaoDasApresentacoes.CompetenciaMensal,
        ExercicioAnual => CriterioDeDistincaoDasApresentacoes.ExercicioAnual,
        Ocorrencia => CriterioDeDistincaoDasApresentacoes.Ocorrencia,
        _ => CriterioDeDistincaoDasApresentacoes.Nenhuma,
    };
}
