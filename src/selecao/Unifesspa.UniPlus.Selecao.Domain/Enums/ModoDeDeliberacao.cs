namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>Como a banca delibera: por votação do colegiado ou por ato de um titular.</summary>
public enum ModoDeDeliberacao
{
    /// <summary>Os titulares votam e a decisão sai do critério, do quórum e do encerramento declarados.</summary>
    ColegiadaComVotacao = 1,

    /// <summary>Qualquer titular designado pratica o ato, sem quórum nem votação.</summary>
    AtoIndividual = 2,
}
