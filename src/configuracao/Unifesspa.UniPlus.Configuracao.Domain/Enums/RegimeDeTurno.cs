namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Regime de turno de uma <see cref="Entities.OfertaCurso"/> (UNI-REQ-0137, ADR-0126): declara se a oferta funciona em um
/// único turno (<see cref="Regular"/>) ou em dois turnos distintos (<see cref="Integral"/>). É atributo <b>obrigatório</b>
/// da oferta, em todo formato pedagógico. A ordem de declaração é a de exibição.
/// </summary>
/// <remarks>
/// O regime é <b>declarado</b>, não inferido da quantidade de turnos informada: o agregado recusa a incoerência em vez de
/// promover a oferta a <see cref="Integral"/> por conta própria.
/// </remarks>
public enum RegimeDeTurno
{
    /// <summary>Oferta em um único turno.</summary>
    [CodigoDeContrato("REGULAR", "Regular")]
    Regular = 1,

    /// <summary>Oferta em dois turnos distintos.</summary>
    [CodigoDeContrato("INTEGRAL", "Integral")]
    Integral = 2,
}
