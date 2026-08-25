namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Período do dia em que uma <see cref="Entities.OfertaCurso"/> funciona
/// (UNI-REQ-0137, ADR-0126). A oferta declara de um a dois turnos, conforme o
/// <see cref="RegimeDeTurno"/> — nenhuma oferta funciona sem turno, em qualquer
/// formato pedagógico. Persistido como token UPPER_SNAKE
/// (<see cref="TurnosOferta"/>).
/// </summary>
public enum TurnoOferta
{
    /// <summary>Sentinela — indica entrada inválida/corrupção se encontrado em runtime.</summary>
    Nenhum = 0,

    /// <summary>Turno matutino.</summary>
    Matutino = 1,

    /// <summary>Turno vespertino.</summary>
    Vespertino = 2,

    /// <summary>Turno noturno.</summary>
    Noturno = 3,
}
