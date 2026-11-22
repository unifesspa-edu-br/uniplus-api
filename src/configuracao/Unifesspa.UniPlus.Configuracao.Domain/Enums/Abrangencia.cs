namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Abrangência territorial de um dia não útil no calendário de contagem de prazos (recurso administrativo e efeito suspensivo, UNI-REQ-0080).
/// Quais entradas de abrangência estadual e municipal incidem num certame deriva da
/// localidade regente que ele congela — é do código IBGE dela que saem a UF e o município
/// (UNI-REQ-0111). A ordem de declaração é a de exibição.
/// </summary>
public enum Abrangencia
{
    /// <summary>Feriado nacional — vale para todo o território.</summary>
    [CodigoDeContrato("NACIONAL", "Nacional")]
    Nacional = 1,

    /// <summary>Feriado estadual — vale só no estado declarado.</summary>
    [CodigoDeContrato("ESTADUAL", "Estadual")]
    Estadual = 2,

    /// <summary>Feriado municipal — vale só no município declarado (exige cópia do código IBGE, nome e UF).</summary>
    [CodigoDeContrato("MUNICIPAL", "Municipal")]
    Municipal = 3,

    /// <summary>Recesso institucional da Unifesspa, sem correspondência em calendário civil.</summary>
    [CodigoDeContrato("INSTITUCIONAL", "Institucional")]
    Institucional = 4,
}
