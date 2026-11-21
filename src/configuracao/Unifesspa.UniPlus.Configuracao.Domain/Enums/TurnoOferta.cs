namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Período do dia em que uma <see cref="Entities.OfertaCurso"/> funciona (UNI-REQ-0137, ADR-0126). A oferta declara de um a
/// dois turnos, conforme o <see cref="RegimeDeTurno"/> — nenhuma oferta funciona sem turno, em qualquer formato pedagógico.
/// A ordem de declaração é a de exibição e a ordem canônica do dia.
/// </summary>
public enum TurnoOferta
{
    /// <summary>Turno matutino.</summary>
    [CodigoDeContrato("MATUTINO", "Matutino")]
    Matutino = 1,

    /// <summary>Turno vespertino.</summary>
    [CodigoDeContrato("VESPERTINO", "Vespertino")]
    Vespertino = 2,

    /// <summary>Turno noturno.</summary>
    [CodigoDeContrato("NOTURNO", "Noturno")]
    Noturno = 3,
}
