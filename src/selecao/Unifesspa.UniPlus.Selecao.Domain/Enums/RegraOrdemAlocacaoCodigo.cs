namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Código canônico da regra de <c>tipo=regra_ordem_alocacao</c> do
/// <c>rol_de_regras</c> (Story #772) — ordem de alocação 1ª/2ª opção →
/// remanejamento → lista de espera (RN04, modelagem P-B §2.9).
/// </summary>
public static class RegraOrdemAlocacaoCodigo
{
    /// <summary>Classifica por nota decrescente; aloca na 1ª opção se há vaga, senão na 2ª; remanejamento; lista de espera (args: <c>n_opcoes</c>).</summary>
    public const string AlocacaoOpcoesRn04 = "ALOCACAO-OPCOES-RN04";
}
