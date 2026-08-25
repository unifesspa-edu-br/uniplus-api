namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Regime de turno de uma <see cref="Entities.OfertaCurso"/> (UNI-REQ-0137,
/// ADR-0126): declara se a oferta funciona em um único turno
/// (<see cref="Regular"/>) ou em dois turnos distintos (<see cref="Integral"/>).
/// É atributo <b>obrigatório</b> da oferta, em todo formato pedagógico.
/// Persistido como token UPPER_SNAKE (<see cref="RegimesDeTurno"/>).
/// </summary>
/// <remarks>
/// O regime é <b>declarado</b>, não inferido da quantidade de turnos informada:
/// o agregado recusa a incoerência em vez de promover a oferta a
/// <see cref="Integral"/> por conta própria.
/// </remarks>
public enum RegimeDeTurno
{
    /// <summary>Sentinela — indica entrada inválida/corrupção se encontrado em runtime.</summary>
    Nenhum = 0,

    /// <summary>Oferta em um único turno.</summary>
    Regular = 1,

    /// <summary>Oferta em dois turnos distintos.</summary>
    Integral = 2,
}
