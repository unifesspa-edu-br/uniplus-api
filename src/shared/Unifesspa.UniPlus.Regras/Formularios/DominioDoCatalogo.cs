namespace Unifesspa.UniPlus.Regras.Formularios;

/// <summary>Os tokens do catálogo de fatos que a Seleção congela sem alcançar os enums da Configuração.</summary>
public static class DominioDoCatalogo
{
    /// <summary>
    /// As hipóteses legais de tratamento no vocabulário fechado da Configuração (LGPD, arts. 7º e 11), para
    /// quem congela o token sem alcançar o enum do catálogo.
    /// </summary>
    public static readonly IReadOnlyList<string> HipotesesLegais =
    [
        "CONSENTIMENTO", "CUMPRIMENTO_OBRIGACAO_LEGAL", "EXECUCAO_POLITICAS_PUBLICAS", "ESTUDOS_POR_ORGAO_DE_PESQUISA",
        "EXECUCAO_CONTRATO", "EXERCICIO_REGULAR_DE_DIREITOS", "PROTECAO_DA_VIDA", "TUTELA_DA_SAUDE", "INTERESSE_LEGITIMO",
        "PROTECAO_DO_CREDITO", "PREVENCAO_A_FRAUDE",
    ];

    /// <summary>
    /// Os componentes de exibição que um fato declara, no vocabulário fechado da Configuração, para quem
    /// congela o token sem alcançar o enum do catálogo.
    /// </summary>
    public static readonly IReadOnlyList<string> ComponentesDeExibicao = ["MOEDA", "SITUACAO"];
}
