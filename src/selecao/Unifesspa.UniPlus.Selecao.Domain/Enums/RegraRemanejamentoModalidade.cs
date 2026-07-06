namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Regra de remanejamento da modalidade quando suas vagas não são preenchidas
/// (snapshot-copy do <c>RegraRemanejamento</c> de <c>Modalidade</c>, ADR-0061).
/// <see cref="Nenhuma"/> é o valor de modalidades que não remanejam (ex.:
/// ampla concorrência, suplementares institucionais).
/// </summary>
public enum RegraRemanejamentoModalidade
{
    /// <summary>Modalidade não remaneja (ex.: AC, suplementar institucional).</summary>
    Nenhuma = 0,

    /// <summary>Segue a cascata legal 8×7 da Lei 12.711 — obrigatória para <see cref="NaturezaLegalModalidade.CotaReservada"/> (INV-12).</summary>
    SegueCascata = 1,

    /// <summary>Remaneja para um destino único e fixo (<see cref="ModalidadeSelecionada.RemanejamentoDestino"/>).</summary>
    DestinoUnico = 2,

    /// <summary>Remanejamento cruzado com par + fallback (ex.: PSIQ IND↔QUIL).</summary>
    Cruzado = 3,
}
