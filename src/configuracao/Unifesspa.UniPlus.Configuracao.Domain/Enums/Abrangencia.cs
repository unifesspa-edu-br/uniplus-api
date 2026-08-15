namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Abrangência territorial de um dia não útil no calendário de contagem de prazos (RN13).
/// Quais entradas de abrangência estadual e municipal incidem num certame deriva da
/// localidade regente que ele congela — é do código IBGE dela que saem a UF e o município
/// (UNI-REQ-0111). Persistida como token UPPER_SNAKE (<see cref="Abrangencias"/>).
/// </summary>
public enum Abrangencia
{
    /// <summary>Sentinela — indica entrada inválida/corrupção se encontrado em runtime.</summary>
    Nenhuma = 0,

    /// <summary>Feriado nacional — vale para todo o território.</summary>
    Nacional = 1,

    /// <summary>Feriado estadual — vale só no estado declarado.</summary>
    Estadual = 2,

    /// <summary>Feriado municipal — vale só no município declarado (exige snapshot de código IBGE, nome e UF).</summary>
    Municipal = 3,

    /// <summary>Recesso institucional da Unifesspa, sem correspondência em calendário civil.</summary>
    Institucional = 4,
}
