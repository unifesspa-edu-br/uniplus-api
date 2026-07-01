namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Natureza jurídica de uma <see cref="Entities.Modalidade"/> de concorrência
/// (UNI-REQ-0011): distingue, em domínio fechado, reserva de cota, ampla
/// concorrência, modalidade suplementar e outra modalidade (Lei 12.711/2012 atual.
/// Lei 14.723/2023). Persistida como token UPPER_SNAKE (<see cref="NaturezasLegais"/>).
/// </summary>
public enum NaturezaLegal
{
    /// <summary>Sentinela — indica entrada inválida/corrupção se encontrado em runtime.</summary>
    Nenhuma = 0,

    /// <summary>Cota reservada por ação afirmativa (segue a cascata de remanejamento).</summary>
    CotaReservada = 1,

    /// <summary>Ampla concorrência — aberta a todos; não é reserva, não é remanejada como cota.</summary>
    Ampla = 2,

    /// <summary>Modalidade suplementar (ex.: PcD em ampla concorrência).</summary>
    Suplementar = 3,

    /// <summary>Outra modalidade que não se enquadra nas anteriores.</summary>
    OutraModalidade = 4,
}
