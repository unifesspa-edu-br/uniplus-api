namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Turno de funcionamento de uma <see cref="Entities.OfertaCurso"/> (story #588,
/// ADR-0066). Atributo <b>opcional</b> da oferta (nem toda oferta declara turno —
/// ex.: EaD); quando presente, é um dos quatro valores do domínio fechado.
/// Persistido como token UPPER_SNAKE (<see cref="TurnosOferta"/>).
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

    /// <summary>Turno integral (manhã e tarde).</summary>
    Integral = 4,
}
