namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Mapeamento único entre <see cref="TipoArestaGrafo"/> e o código textual canônico UPPER_SNAKE que
/// entra no bloco <c>grafoDependencia</c> do envelope de publicação (Story #928, §7). Fonte de
/// verdade única do wire format — o codec do envelope consome estes métodos, nunca
/// <c>enum.ToString()</c> (que produziria <c>Producao</c> em vez de <c>PRODUCAO</c>).
/// </summary>
public static class TipoArestaGrafoCodigo
{
    public const string Producao = "PRODUCAO";
    public const string Precondicao = "PRECONDICAO";
    public const string Derivacao = "DERIVACAO";
    public const string Gatilho = "GATILHO";

    /// <summary>
    /// Converte a classe de aresta para o código canônico. O <c>switch</c> é exaustivo: uma 5ª
    /// classe quebra a build (CS8509 promovido a erro por <c>TreatWarningsAsErrors</c>) até este
    /// mapeamento absorvê-la.
    /// </summary>
    public static string ToCodigo(this TipoArestaGrafo tipo) => tipo switch
    {
        TipoArestaGrafo.Producao => Producao,
        TipoArestaGrafo.Precondicao => Precondicao,
        TipoArestaGrafo.Derivacao => Derivacao,
        TipoArestaGrafo.Gatilho => Gatilho,
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Classe de aresta do grafo desconhecida."),
    };
}
