namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Domínio (tipo de dado) de um <see cref="Entities.FatoCandidato"/> — decide quais operadores e
/// que forma de valor um predicado sobre o fato aceita (ADR-0111). De onde vêm os valores do
/// categórico não é outro domínio: é a <see cref="FonteValoresFato"/> (ADR-0136).
/// </summary>
public enum DominioFato
{
    /// <summary>Sentinela — domínio não informado; rejeitado na criação.</summary>
    Nenhum = 0,

    /// <summary>Conjunto fechado de códigos; de onde vêm os valores diz a fonte dos valores.</summary>
    Categorico,

    /// <summary>Verdadeiro/falso.</summary>
    Booleano,

    /// <summary>Escalar numérico inteiro.</summary>
    Numerico,
}
