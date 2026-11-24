namespace Unifesspa.UniPlus.Regras.Formularios;

/// <summary>Os tokens do catálogo de fatos que a Seleção congela sem alcançar os enums da Configuração.</summary>
public static class DominioDoCatalogo
{
    /// <summary>
    /// Os componentes de exibição que um fato declara, no vocabulário fechado da Configuração, para quem
    /// congela o token sem alcançar o enum do catálogo.
    /// </summary>
    public static readonly IReadOnlyList<string> ComponentesDeExibicao = ["MOEDA", "SITUACAO"];
}
