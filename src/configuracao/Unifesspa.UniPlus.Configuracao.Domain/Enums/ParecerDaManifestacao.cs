namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// O parecer da manifestação técnica do Gestor de proteção de dados sobre a proteção de um tipo de documento ou fato do
/// candidato (UNI-REQ-0373).
/// A manifestação não decide: quem decide é o Representante do controlador (ADR-0162).
/// </summary>
public enum ParecerDaManifestacao
{
    /// <summary>Sentinela — parecer não informado.</summary>
    Nenhum = 0,

    /// <summary>Recomenda a aprovação da proteção declarada.</summary>
    RecomendaAprovacao,

    /// <summary>A proteção precisa de ajuste; o texto diz qual.</summary>
    RecomendaAjuste,

    /// <summary>Recomenda não usar o item; o texto diz por quê.</summary>
    RecomendaNaoUso,
}
