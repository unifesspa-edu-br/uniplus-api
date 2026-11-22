namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Natureza de uma <see cref="Entities.Modalidade"/> de concorrência (UNI-REQ-0141):
/// distingue, em domínio fechado, a cota da Lei 12.711/2012 (atual. Lei 14.723/2023),
/// a ação afirmativa instituída por norma da universidade e a ampla concorrência.
/// A ordem de declaração é a de exibição.
/// </summary>
public enum NaturezaLegal
{
    /// <summary>Cota da Lei 12.711/2012, condicionada à escola pública (segue a cascata de remanejamento).</summary>
    [CodigoDeContrato("COTA_RESERVADA", "Cota reservada")]
    CotaReservada = 1,

    /// <summary>Ampla concorrência — aberta a todos; não é reserva, não é remanejada como cota.</summary>
    [CodigoDeContrato("AMPLA", "Ampla concorrência")]
    Ampla = 2,

    /// <summary>
    /// Ação afirmativa institucional (Res. Unifesspa 532/2021), sem condição de escola
    /// pública nem de renda — ex.: pessoa com deficiência na ampla concorrência, vagas
    /// por acréscimo para indígenas e quilombolas. Não convive com cota da lei na mesma
    /// inscrição, salvo a de pessoa com deficiência (UNI-REQ-0142 e UNI-REQ-0266).
    /// </summary>
    [CodigoDeContrato("ACAO_AFIRMATIVA", "Ação afirmativa")]
    AcaoAfirmativa = 3,
}
