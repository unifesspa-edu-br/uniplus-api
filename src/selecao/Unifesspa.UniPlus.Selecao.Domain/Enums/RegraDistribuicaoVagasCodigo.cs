namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Códigos canônicos das regras de <c>tipo=regra_distribuicao_vagas</c> do
/// <c>rol_de_regras</c> (Story #772) que o domínio de distribuição de vagas
/// (Story #773) precisa reconhecer para aplicar invariantes específicas —
/// ex.: só a Lei 12.711 exige referência demográfica (INV-5) e as 8
/// modalidades federais + AC (INV-6).
/// </summary>
public static class RegraDistribuicaoVagasCodigo
{
    /// <summary>Distribuição pela Lei 12.711/2012 (art. 10, red. Lei 14.723/2023) — percentuais demográficos + garantias mínimas.</summary>
    public const string Lei12711 = "DISTRIB-VAGAS-LEI-12711";

    /// <summary>Distribuição institucional (Res. Unifesspa 532/2021 + art. 12) — quadro fixo por edital, fora do art. 10.</summary>
    public const string Institucional = "DISTRIB-VAGAS-INSTITUCIONAL";
}
