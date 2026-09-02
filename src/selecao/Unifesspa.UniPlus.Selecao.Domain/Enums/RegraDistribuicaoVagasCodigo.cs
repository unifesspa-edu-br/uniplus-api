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

    /// <summary>Processo Seletivo Indígena e Quilombola — quadro fixo, exclusivo de vagas por acréscimo.</summary>
    public const string Psiq = "DISTRIB-VAGAS-PSIQ";

    /// <summary>PSE Educação do Campo — quadro fixo, ampla concorrência e reserva de PcD sem as cotas federais.</summary>
    public const string EduCampo = "DISTRIB-VAGAS-EDU-CAMPO";

    /// <summary>
    /// Regras cujo quadro é <b>fixado pelo edital</b>, e não calculado pelo art. 10. Todas
    /// compartilham a mesma montagem: as quantidades declaradas dividem o VO_base.
    /// </summary>
    public static bool EhQuadroFixo(string codigo) =>
        codigo is Institucional or Psiq or EduCampo;
}
