namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// O parecer da manifestação técnica do Encarregado de dados sobre a proteção de um tipo de documento ou fato do
/// candidato (UNI-REQ-0373).
/// A manifestação não decide: quem decide é o Representante do controlador (ADR-0162).
/// </summary>
public enum ParecerDaManifestacao
{
    /// <summary>Sentinela — parecer não informado.</summary>
    Nenhum = 0,

    /// <summary>A proteção declarada está adequada.</summary>
    Adequada,

    /// <summary>A proteção precisa de ajuste; o texto diz qual.</summary>
    RecomendaAjuste,

    /// <summary>A proteção está inadequada; o texto diz por quê.</summary>
    Inadequada,
}
