namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Os 8 códigos de modalidade federal da Lei 12.711/2012 (red. Lei 14.723/2023,
/// art. 3º) + a ampla concorrência — fato legal fixo, não configurável.
/// Consumido pela INV-6 da modelagem P-A: quando a distribuição aplica
/// <see cref="RegraDistribuicaoVagasCodigo.Lei12711"/>, as 8 modalidades
/// federais e a AC são obrigatoriamente selecionadas.
/// </summary>
public static class ModalidadesFederaisLei12711
{
    public const string LbPpi = "LB_PPI";
    public const string LbQ = "LB_Q";
    public const string LbPcd = "LB_PCD";
    public const string LbEp = "LB_EP";
    public const string LiPpi = "LI_PPI";
    public const string LiQ = "LI_Q";
    public const string LiPcd = "LI_PCD";
    public const string LiEp = "LI_EP";

    /// <summary>Ampla concorrência — sempre obrigatória junto das 8 federais.</summary>
    public const string Ac = "AC";

    /// <summary>Os 8 códigos federais (sem AC) — ordem I a VIII (art. 10, §2º).</summary>
    public static readonly IReadOnlyList<string> Codigos =
        [LbPpi, LbQ, LbPcd, LbEp, LiPpi, LiQ, LiPcd, LiEp];

    /// <summary>Os 8 códigos federais + AC — conjunto completo exigido pela INV-6.</summary>
    public static readonly IReadOnlyList<string> CodigosComAc = [.. Codigos, Ac];
}
