namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Os atos do circuito de aprovação da proteção de dados de um item do catálogo (ADR-0153) que ficam
/// registrados de forma permanente.
/// </summary>
public enum AtoDaProtecao
{
    /// <summary>Sentinela — ato não informado.</summary>
    Nenhum = 0,

    /// <summary>O administrador enviou o item à proteção de dados.</summary>
    Solicitacao,

    /// <summary>A proteção de dados aprovou a proteção conferida.</summary>
    Aprovacao,

    /// <summary>A proteção de dados recomendou a classificação correta.</summary>
    AjusteRecomendado,

    /// <summary>A proteção de dados reprovou a proteção, com a justificativa.</summary>
    Reprovacao,

    /// <summary>O administrador aceitou a classificação recomendada.</summary>
    Aceite,
}
