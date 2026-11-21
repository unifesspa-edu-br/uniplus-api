namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Regime de funcionamento de uma <see cref="Entities.OfertaCurso"/> (UNI-REQ-0138, ADR-0128): declara se a oferta
/// funciona de forma <see cref="Intensivo"/>, <see cref="Extensivo"/> ou <see cref="AlternanciaPedagogica"/>. É atributo
/// <b>obrigatório</b> da oferta e dimensão <b>própria</b> — não substitui nem reutiliza o <see cref="RegimeDeTurno"/>, o
/// <see cref="ProgramaDeOferta"/>, o <see cref="FormatoPedagogico"/> nem o <see cref="TurnoOferta"/>. A ordem de
/// declaração é a de exibição.
/// </summary>
/// <remarks>
/// O regime é <b>declarado</b>, nunca inferido da quantidade de turnos, do regime de turno, do formato pedagógico ou do
/// programa: o agregado recusa a combinação incompatível em vez de converter uma dimensão para acomodar a outra. A única
/// regra de compatibilidade é a de <see cref="RegimesDeFuncionamento.RegimeDeTurnoExigido"/>: a oferta
/// <see cref="Intensivo"/> exige regime de turno <see cref="RegimeDeTurno.Integral"/>.
/// </remarks>
public enum RegimeDeFuncionamento
{
    /// <summary>Oferta intensiva — exige regime de turno integral.</summary>
    [CodigoDeContrato("INTENSIVO", "Intensivo")]
    Intensivo = 1,

    /// <summary>Oferta extensiva — aceita regime de turno regular ou integral.</summary>
    [CodigoDeContrato("EXTENSIVO", "Extensivo")]
    Extensivo = 2,

    /// <summary>Oferta em alternância pedagógica — aceita regime de turno regular ou integral.</summary>
    [CodigoDeContrato("ALTERNANCIA_PEDAGOGICA", "Alternância pedagógica")]
    AlternanciaPedagogica = 3,
}
