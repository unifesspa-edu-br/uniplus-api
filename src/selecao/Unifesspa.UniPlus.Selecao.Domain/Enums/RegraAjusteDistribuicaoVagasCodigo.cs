namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Código canônico da regra de <c>tipo=regra_ajuste_distribuicao_vagas</c> do
/// <c>rol_de_regras</c> que rege a capagem em <c>VO</c> e a prioridade legal
/// entre sub-reservas na escassez (art. 11 §único) — referenciada e congelada
/// pela <see cref="Entities.ConfiguracaoDistribuicaoVagas"/> no ramo federal
/// (issue #848/ADR-0115).
/// </summary>
public static class RegraAjusteDistribuicaoVagasCodigo
{
    /// <summary>Reconciliação do estouro por arredondamento — art. 11 §único (Portaria MEC 18/2012).</summary>
    public const string ReconciliacaoArt11ParagrafoUnico = "RECONCILIACAO-VAGAS-ART11-PU";
}
