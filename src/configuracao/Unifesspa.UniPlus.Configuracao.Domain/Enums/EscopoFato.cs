namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Sobre quem o <see cref="Entities.FatoCandidato"/> é respondido (ADR-0136): o próprio
/// candidato, ou cada membro de uma lista do formulário, como a composição familiar (ADR-0138).
/// </summary>
public enum EscopoFato
{
    /// <summary>Sentinela — escopo não informado; rejeitado na criação.</summary>
    Nenhum = 0,

    /// <summary>O fato é respondido sobre o candidato.</summary>
    Candidato,

    /// <summary>O fato é respondido sobre cada membro de um grupo repetível.</summary>
    MembroGrupo,
}
