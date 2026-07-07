namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Códigos canônicos das regras de <c>tipo=regra_calculo</c> do
/// <c>rol_de_regras</c> (Story #772) reconhecidas pela configuração de
/// classificação (Story #775, modelagem P-B §2.3).
/// </summary>
public static class RegraCalculoCodigo
{
    /// <summary>Nota = Σ(nota_etapa × peso) / Σ(peso) — divisor derivado das etapas que compõem a nota.</summary>
    public const string FormulaMediaPonderada = "FORMULA-MEDIA-PONDERADA";

    /// <summary>Sem cálculo local — classificação vem do sistema federal (SiSU/MEC). INV-B8: dispensa arredondamento e eliminação locais.</summary>
    public const string ClassificacaoImportada = "CLASSIFICACAO-IMPORTADA";
}
