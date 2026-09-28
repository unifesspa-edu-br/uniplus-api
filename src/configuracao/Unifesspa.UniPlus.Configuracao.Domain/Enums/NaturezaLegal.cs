namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Natureza de uma <see cref="Entities.Modalidade"/> de concorrência (UNI-REQ-0141):
/// distingue, em domínio fechado, a cota da Lei 12.711/2012 (atual. Lei 14.723/2023),
/// a ação afirmativa instituída por norma da universidade e a ampla concorrência.
/// Persistida como token UPPER_SNAKE (<see cref="NaturezasLegais"/>).
/// </summary>
public enum NaturezaLegal
{
    /// <summary>Sentinela — indica entrada inválida/corrupção se encontrado em runtime.</summary>
    Nenhuma = 0,

    /// <summary>Cota da Lei 12.711/2012, condicionada à escola pública (segue a cascata de remanejamento).</summary>
    CotaReservada = 1,

    /// <summary>Ampla concorrência — aberta a todos; não é reserva, não é remanejada como cota.</summary>
    Ampla = 2,

    /// <summary>
    /// Ação afirmativa institucional (Res. Unifesspa 532/2021), sem condição de escola
    /// pública nem de renda — ex.: pessoa com deficiência na ampla concorrência, vagas
    /// por acréscimo para indígenas e quilombolas. Não convive com cota da lei na mesma
    /// inscrição (UNI-REQ-0142).
    /// </summary>
    AcaoAfirmativa = 3,
}
