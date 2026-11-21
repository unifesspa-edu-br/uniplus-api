namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Programa (política pública ou convênio) sob o qual uma <see cref="Entities.OfertaCurso"/> é ofertada (ADR-0066): separa
/// a oferta regular das ofertas vinculadas a programas especiais e a convênios. A ordem de declaração é a de exibição.
/// </summary>
public enum ProgramaDeOferta
{
    /// <summary>Oferta regular da instituição, sem programa especial.</summary>
    [CodigoDeContrato("REGULAR", "Regular")]
    Regular = 1,

    /// <summary>Programa Forma Pará, de interiorização estadual.</summary>
    [CodigoDeContrato("FORMA_PARA", "Forma Pará")]
    FormaPara = 2,

    /// <summary>Plano Nacional de Formação de Professores da Educação Básica.</summary>
    [CodigoDeContrato("PARFOR", "Parfor")]
    Parfor = 3,

    /// <summary>Programa Nacional de Educação na Reforma Agrária.</summary>
    [CodigoDeContrato("PRONERA", "Pronera")]
    Pronera = 4,

    /// <summary>Programa de Estudantes-Convênio PEPETI.</summary>
    [CodigoDeContrato("PEPETI", "PEPETI")]
    Pepeti = 5,

    /// <summary>Oferta vinculada a outro convênio institucional.</summary>
    [CodigoDeContrato("CONVENIO_OUTRO", "Outro convênio")]
    OutroConvenio = 6,

    /// <summary>Outro programa que não se enquadra nos anteriores.</summary>
    [CodigoDeContrato("OUTRO", "Outro programa")]
    Outro = 7,
}
