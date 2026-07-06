namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Mapeamento único entre <see cref="TipoRegra"/> e o código textual canônico
/// snake_case do <c>rol_de_regras</c> (o valor persistido na coluna <c>tipo</c>
/// e o token que entra no hash content-addressable). Fonte de verdade única do
/// wire format — o EF converter e o computador de hash consomem estes métodos,
/// nunca <c>enum.ToString()</c>.
/// </summary>
public static class TipoRegraCodigo
{
    public const string RegraCalculo = "regra_calculo";
    public const string RegraArredondamento = "regra_arredondamento";
    public const string RegraEliminacao = "regra_eliminacao";
    public const string RegraBonus = "regra_bonus";
    public const string CriterioDesempate = "criterio_desempate";
    public const string CriterioRemanejamento = "criterio_remanejamento";
    public const string RegraOrdemAlocacao = "regra_ordem_alocacao";
    public const string RegraElegibilidade = "regra_elegibilidade";
    public const string RegraDistribuicaoVagas = "regra_distribuicao_vagas";
    public const string RegraAjusteDistribuicaoVagas = "regra_ajuste_distribuicao_vagas";
    public const string RegraPrazoRecurso = "regra_prazo_recurso";

    /// <summary>
    /// Converte o tipo para o código canônico. O <c>switch</c> é exaustivo: uma
    /// 12ª variante quebra a build (CS8509 promovido a erro por
    /// <c>TreatWarningsAsErrors</c>) até este mapeamento absorvê-la.
    /// </summary>
    public static string ToCodigo(this TipoRegra tipo) => tipo switch
    {
        TipoRegra.RegraCalculo => RegraCalculo,
        TipoRegra.RegraArredondamento => RegraArredondamento,
        TipoRegra.RegraEliminacao => RegraEliminacao,
        TipoRegra.RegraBonus => RegraBonus,
        TipoRegra.CriterioDesempate => CriterioDesempate,
        TipoRegra.CriterioRemanejamento => CriterioRemanejamento,
        TipoRegra.RegraOrdemAlocacao => RegraOrdemAlocacao,
        TipoRegra.RegraElegibilidade => RegraElegibilidade,
        TipoRegra.RegraDistribuicaoVagas => RegraDistribuicaoVagas,
        TipoRegra.RegraAjusteDistribuicaoVagas => RegraAjusteDistribuicaoVagas,
        TipoRegra.RegraPrazoRecurso => RegraPrazoRecurso,
        TipoRegra.Nenhuma => throw new ArgumentOutOfRangeException(
            nameof(tipo), tipo, "TipoRegra.Nenhuma é sentinela e não tem código canônico."),
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "TipoRegra desconhecido."),
    };

    /// <summary>
    /// Converte o código canônico de volta para o tipo. Usado na
    /// materialização EF (leitura da coluna <c>tipo</c>).
    /// </summary>
    public static TipoRegra FromCodigo(string codigo) => codigo switch
    {
        RegraCalculo => TipoRegra.RegraCalculo,
        RegraArredondamento => TipoRegra.RegraArredondamento,
        RegraEliminacao => TipoRegra.RegraEliminacao,
        RegraBonus => TipoRegra.RegraBonus,
        CriterioDesempate => TipoRegra.CriterioDesempate,
        CriterioRemanejamento => TipoRegra.CriterioRemanejamento,
        RegraOrdemAlocacao => TipoRegra.RegraOrdemAlocacao,
        RegraElegibilidade => TipoRegra.RegraElegibilidade,
        RegraDistribuicaoVagas => TipoRegra.RegraDistribuicaoVagas,
        RegraAjusteDistribuicaoVagas => TipoRegra.RegraAjusteDistribuicaoVagas,
        RegraPrazoRecurso => TipoRegra.RegraPrazoRecurso,
        _ => throw new ArgumentOutOfRangeException(
            nameof(codigo), codigo, "Código de tipo de regra desconhecido."),
    };
}
