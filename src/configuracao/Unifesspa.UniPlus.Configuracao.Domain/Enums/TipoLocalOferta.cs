namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Classificação de um <c>LocalOferta</c> quanto à modalidade de oferta de vagas
/// Conjunto fechado — novos tipos exigem deliberação do líder técnico.
/// O código de contrato de cada membro nunca muda. A ordem de declaração é a de exibição.
/// </summary>
public enum TipoLocalOferta
{
    /// <summary>Campus sede da instituição.</summary>
    [CodigoDeContrato("CAMPUS_SEDE", "Campus sede")]
    CampusSede = 1,

    /// <summary>Campus fora de sede (interiorização).</summary>
    [CodigoDeContrato("CAMPUS_FORA_DE_SEDE", "Campus fora de sede")]
    CampusForaDeSede = 2,

    /// <summary>Oferta de curso fora de sede vinculada a um campus.</summary>
    [CodigoDeContrato("CURSO_FORA_DE_SEDE", "Curso fora de sede")]
    CursoForaDeSede = 3,

    /// <summary>Polo de educação a distância.</summary>
    [CodigoDeContrato("POLO_DE_EDUCACAO_A_DISTANCIA", "Polo de educação a distância")]
    PoloDeEducacaoADistancia = 4,

    /// <summary>Local de oferta resultante de convênio de interiorização.</summary>
    [CodigoDeContrato("CONVENIO_DE_INTERIORIZACAO", "Convênio de interiorização")]
    ConvenioDeInteriorizacao = 5,

    /// <summary>Outro tipo de local de oferta não enquadrado nos demais.</summary>
    [CodigoDeContrato("OUTRO", "Outro")]
    Outro = 6,
}
