namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>Se o candidato fez a prova de uma área do ENEM.</summary>
public enum SituacaoDaProvaNoEnem
{
    Presente = 1,

    /// <summary>Faltou à prova da área. A nota que vem no arquivo é zero e não vale como nota obtida.</summary>
    Ausente = 2,
}
