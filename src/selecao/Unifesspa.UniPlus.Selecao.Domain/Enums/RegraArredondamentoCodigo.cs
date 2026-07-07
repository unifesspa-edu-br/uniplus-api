namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Códigos canônicos das regras de <c>tipo=regra_arredondamento</c> do
/// <c>rol_de_regras</c> (Story #772) — precisão da nota (modelagem P-B §2.4).
/// </summary>
public static class RegraArredondamentoCodigo
{
    /// <summary>Trunca na N-ésima casa, sem arredondar — default de todos (gaps 1.1).</summary>
    public const string PrecisaoTruncar = "PRECISAO-TRUNCAR";

    /// <summary>Arredonda para cima se a próxima casa ≥ 5 — só para reproduzir editais antigos.</summary>
    public const string PrecisaoArredondarCima = "PRECISAO-ARREDONDAR-CIMA";
}
