namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Natureza jurídica da modalidade selecionada para a distribuição de vagas de
/// uma oferta (snapshot-copy do <c>NaturezaLegal</c> de <c>Modalidade</c>,
/// ADR-0061). Determina se a concorrência dupla (Lei 14.723/2023) se aplica —
/// obrigatória quando ao menos uma modalidade selecionada é
/// <see cref="CotaReservada"/>.
/// </summary>
public enum NaturezaLegalModalidade
{
    Nenhuma = 0,

    /// <summary>Cota da Lei 12.711/2012 (reservada) — dispara concorrência dupla.</summary>
    CotaReservada = 1,

    /// <summary>Ampla concorrência (o residual do VO_base após as reservas).</summary>
    Ampla = 2,

    /// <summary>
    /// Ação afirmativa institucional, fora da Lei 12.711 — a PcD "V" retirada da AC e as
    /// vagas por acréscimo do PSIQ. Não convive com cota da lei na mesma inscrição
    /// (UNI-REQ-0142).
    /// </summary>
    AcaoAfirmativa = 3,
}
