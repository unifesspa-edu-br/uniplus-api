namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using System.Collections.Frozen;

/// <summary>
/// Mapeamento único entre <see cref="TipoRegra"/> e o código textual canônico
/// snake_case do <c>regras_do_catalogo</c> (o valor persistido na coluna <c>tipo</c>
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
    public const string AlgoritmoContagemPrazo = "algoritmo_contagem_prazo";
    public const string RegraConsolidacao = "regra_consolidacao";
    public const string RegraProcedimentoRecurso = "regra_procedimento_recurso";

    private static readonly FrozenDictionary<TipoRegra, string> CodigoPorTipo = new Dictionary<TipoRegra, string>
    {
        [TipoRegra.RegraCalculo] = RegraCalculo,
        [TipoRegra.RegraArredondamento] = RegraArredondamento,
        [TipoRegra.RegraEliminacao] = RegraEliminacao,
        [TipoRegra.RegraBonus] = RegraBonus,
        [TipoRegra.CriterioDesempate] = CriterioDesempate,
        [TipoRegra.CriterioRemanejamento] = CriterioRemanejamento,
        [TipoRegra.RegraOrdemAlocacao] = RegraOrdemAlocacao,
        [TipoRegra.RegraElegibilidade] = RegraElegibilidade,
        [TipoRegra.RegraDistribuicaoVagas] = RegraDistribuicaoVagas,
        [TipoRegra.RegraAjusteDistribuicaoVagas] = RegraAjusteDistribuicaoVagas,
        [TipoRegra.RegraPrazoRecurso] = RegraPrazoRecurso,
        [TipoRegra.AlgoritmoContagemPrazo] = AlgoritmoContagemPrazo,
        [TipoRegra.RegraConsolidacao] = RegraConsolidacao,
        [TipoRegra.RegraProcedimentoRecurso] = RegraProcedimentoRecurso,
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<string, TipoRegra> TipoPorCodigo =
        CodigoPorTipo.ToFrozenDictionary(static par => par.Value, static par => par.Key, StringComparer.Ordinal);

    /// <summary>
    /// Converte o tipo para o código canônico. A tabela nomeia todas as variantes; qualquer valor fora dela, inclusive o
    /// sentinela, lança, e a paridade com <see cref="FromCodigo"/> vem de uma única tabela.
    /// </summary>
    public static string ToCodigo(this TipoRegra tipo) =>
        CodigoPorTipo.TryGetValue(tipo, out string? codigo) ? codigo : throw TipoSemCodigo(tipo);

    /// <summary>
    /// Converte o código canônico de volta para o tipo. Usado na
    /// materialização EF (leitura da coluna <c>tipo</c>).
    /// </summary>
    public static TipoRegra FromCodigo(string codigo) =>
        codigo is not null && TipoPorCodigo.TryGetValue(codigo, out TipoRegra tipo)
            ? tipo
            : throw new ArgumentOutOfRangeException(nameof(codigo), codigo, "Código de tipo de regra desconhecido.");

    private static ArgumentOutOfRangeException TipoSemCodigo(TipoRegra tipo) => tipo == TipoRegra.Nenhuma
        ? new(nameof(tipo), tipo, "TipoRegra.Nenhuma é sentinela e não tem código canônico.")
        : new(nameof(tipo), tipo, "TipoRegra desconhecido.");
}
