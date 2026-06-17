namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Classificação de um <c>LocalOferta</c> quanto à modalidade de oferta de vagas
/// (UNI-REQ #587). Roster fechado — novos tipos exigem deliberação do Tech Lead.
/// O valor numérico faz parte do contrato: nunca renumerar existentes.
/// </summary>
public enum TipoLocalOferta
{
    /// <summary>Sentinela — indica corrupção se encontrado em runtime.</summary>
    Nenhum = 0,

    /// <summary>Campus sede da instituição.</summary>
    CampusSede = 1,

    /// <summary>Campus fora de sede (interiorização).</summary>
    CampusForaDeSede = 2,

    /// <summary>Oferta de curso fora de sede vinculada a um campus.</summary>
    CursoForaDeSede = 3,

    /// <summary>Polo de educação a distância (EaD).</summary>
    PoloEad = 4,

    /// <summary>Local de oferta resultante de convênio de interiorização.</summary>
    ConvenioInteriorizacao = 5,

    /// <summary>Outro tipo de local de oferta não enquadrado nos demais.</summary>
    Outro = 6,
}
