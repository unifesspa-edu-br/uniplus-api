namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Domínio (tipo de dado) de um <see cref="Entities.FatoCandidato"/> — decide
/// quais operadores e que forma de valor um predicado sobre o fato aceita
/// (ADR-0111). São <b>três</b>, fechados: o "texto" não é um domínio, é a
/// ausência de domínio decidível. A distinção estático × escopo-processo de um
/// fato categórico não é um quarto domínio — é o <c>ValoresDominio</c> nulo.
/// </summary>
public enum DominioFato
{
    /// <summary>Sentinela — domínio não informado; rejeitado na criação.</summary>
    Nenhum = 0,

    /// <summary>Conjunto fechado de códigos (estático no catálogo, ou de escopo-processo quando os valores são nulos).</summary>
    Categorico,

    /// <summary>Verdadeiro/falso.</summary>
    Booleano,

    /// <summary>Escalar numérico inteiro.</summary>
    Numerico,
}
