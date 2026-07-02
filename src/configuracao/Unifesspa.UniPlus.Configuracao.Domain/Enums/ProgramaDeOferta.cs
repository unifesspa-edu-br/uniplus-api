namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Programa (política pública ou convênio) sob o qual uma
/// <see cref="Entities.OfertaCurso"/> é ofertada (story #588, ADR-0066):
/// distingue, em domínio fechado, a oferta regular das ofertas vinculadas a
/// programas especiais (Forma Pará, Parfor, Pronera, PEPETI) e a convênios.
/// Persistido como token UPPER_SNAKE (<see cref="ProgramasDeOferta"/>).
/// </summary>
public enum ProgramaDeOferta
{
    /// <summary>Sentinela — indica entrada inválida/corrupção se encontrado em runtime.</summary>
    Nenhum = 0,

    /// <summary>Oferta regular da instituição (sem programa especial).</summary>
    Regular = 1,

    /// <summary>Programa Forma Pará (interiorização estadual).</summary>
    FormaPara = 2,

    /// <summary>Plano Nacional de Formação de Professores da Educação Básica.</summary>
    Parfor = 3,

    /// <summary>Programa Nacional de Educação na Reforma Agrária.</summary>
    Pronera = 4,

    /// <summary>Programa de Estudantes-Convênio PEPETI.</summary>
    Pepeti = 5,

    /// <summary>Oferta vinculada a outro convênio institucional.</summary>
    ConvenioOutro = 6,

    /// <summary>Outro programa que não se enquadra nos anteriores.</summary>
    Outro = 7,
}
