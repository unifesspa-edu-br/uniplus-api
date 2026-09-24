namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Código canônico da regra de <c>tipo=regra_ordem_alocacao</c> do <c>rol_de_regras</c>.
/// </summary>
public static class RegraOrdemAlocacaoCodigo
{
    /// <summary>
    /// A 1ª opção tem prioridade (UNI-REQ-0045): processam-se todas as 1ªs opções de cada curso; a
    /// vaga de modalidade não preenchida é remanejada dentro do curso, ainda entre candidatos de 1ª
    /// opção; só a vaga que sobra vai para quem escolheu o curso como 2ª opção, em ordem de nota, sem
    /// deslocar aprovado de 1ª opção; quem não entra em nenhuma opção vai para a lista de espera
    /// (args: <c>n_opcoes</c>).
    /// </summary>
    public const string AlocacaoPrimeiraOpcaoPrioritaria = "ALOCACAO-PRIMEIRA-OPCAO-PRIORITARIA";
}
