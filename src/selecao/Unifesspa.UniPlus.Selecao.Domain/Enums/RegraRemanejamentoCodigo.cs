namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Código canônico da regra de <c>tipo=criterio_remanejamento</c> do
/// <c>rol_de_regras</c> reconhecida pela cascata de remanejamento (Story #575,
/// RN-CASCATA-5).
/// </summary>
public static class RegraRemanejamentoCodigo
{
    /// <summary>A ordem legal das oito modalidades federais da Lei 12.711/2012 (red. Lei 14.723/2023), com fallback em ampla concorrência.</summary>
    public const string Cascata = "REMANEJ-CASCATA-LEI-12711";
}
