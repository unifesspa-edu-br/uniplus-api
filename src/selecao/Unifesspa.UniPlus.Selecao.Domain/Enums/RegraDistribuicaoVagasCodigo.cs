namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Códigos canônicos das regras de <c>tipo=regra_distribuicao_vagas</c> do
/// <c>rol_de_regras</c> (Story #772) que o domínio de distribuição de vagas
/// (Story #773) precisa reconhecer para aplicar invariantes específicas —
/// ex.: só o ramo federal exige referência demográfica (INV-5) e as 8
/// modalidades federais + AC (INV-6).
/// </summary>
public static class RegraDistribuicaoVagasCodigo
{
    /// <summary>Distribuição pela Lei 12.711/2012 (art. 10, red. Lei 14.723/2023) — percentuais demográficos + garantias mínimas.</summary>
    public const string Lei12711 = "DISTRIB-VAGAS-LEI-12711";

    /// <summary>
    /// A mesma fórmula de <see cref="Lei12711"/> (art. 10) com uma décima modalidade — <c>AC_PCD</c>,
    /// retirada da ampla concorrência — no rol admitido. As 9 modalidades da INV-6 continuam
    /// obrigatórias; a diferença é só a folga adicional que esta variação reconhece.
    /// </summary>
    public const string Lei12711ComAcPcd = "DISTRIB-VAGAS-LEI-12711-COM-AC-PCD";

    /// <summary>Distribuição institucional (Res. Unifesspa 532/2021 + art. 12) — quadro fixo por edital, fora do art. 10.</summary>
    public const string Institucional = "DISTRIB-VAGAS-INSTITUCIONAL";

    /// <summary>Processo Seletivo Indígena e Quilombola — quadro fixo, exclusivo de vagas por acréscimo.</summary>
    public const string Psiq = "DISTRIB-VAGAS-PSIQ";

    /// <summary>Quadro fixo, ampla concorrência e reserva de PcD sem as cotas federais — generaliza o antigo PSE Educação do Campo (UNI-REQ-0085).</summary>
    public const string ComPcdPuro = "DISTRIB-VAGAS-COM-PCD-PURO";

    /// <summary>
    /// Todo código reconhecido — partição fechada com <see cref="EhQuadroFixo"/>: uma regra nova
    /// entra aqui e num dos dois predicados, ou cai no ramo "nem federal nem quadro fixo" que
    /// <see cref="Entities.ConfiguracaoDistribuicaoVagas.Criar"/> trata como preenchimento inerte.
    /// </summary>
    public static readonly IReadOnlyList<string> Todos =
    [
        Lei12711, Lei12711ComAcPcd, Institucional, Psiq, ComPcdPuro,
    ];

    /// <summary>
    /// Regras cujo quadro é <b>calculado</b> pelo art. 10/11 da Lei 12.711/2012 — percentuais
    /// demográficos, garantia mínima por sub-reserva e a reconciliação do art. 11, §único.
    /// </summary>
    public static bool EhRamoFederal(string codigo) =>
        codigo is Lei12711 or Lei12711ComAcPcd;

    /// <summary>
    /// Regras cujo quadro é <b>fixado pelo edital</b>, e não calculado pelo art. 10. Todas
    /// compartilham a mesma montagem: as quantidades declaradas dividem o VO_base.
    /// </summary>
    public static bool EhQuadroFixo(string codigo) =>
        codigo is Institucional or Psiq or ComPcdPuro;

    /// <summary>
    /// O rol exato que cada código de quadro fechado reconhece. A camada de aplicação resolve
    /// o mesmo rol a partir do <c>modalidades_admitidas</c> do catálogo (fonte de verdade); esta
    /// cópia estática serve só a reidratação de um envelope congelado
    /// (<c>EnvelopeCodecV11.LerDistribuicao</c>), que reconstrói o agregado exclusivamente a
    /// partir dos bytes do próprio envelope, sem consultar o catálogo — condição da
    /// reprodutibilidade não-circular (CA-13). <see langword="null"/> para
    /// <see cref="Institucional"/> (rol aberto) e para qualquer código fora dos cinco
    /// reconhecidos.
    /// </summary>
    public static IReadOnlyList<string>? RolFechado(string codigo) => codigo switch
    {
        Lei12711 => ModalidadesFederaisLei12711.CodigosComAc,
        Lei12711ComAcPcd => [.. ModalidadesFederaisLei12711.CodigosComAc, "AC_PCD"],
        Psiq => ["AC_I", "AC_Q"],
        ComPcdPuro => ["AC", "PCD_PURO"],
        _ => null,
    };
}
