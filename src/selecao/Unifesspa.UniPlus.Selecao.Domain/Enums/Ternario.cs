namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Resultado ternário fail-closed da avaliação de um <see cref="ValueObjects.PredicadoDnf"/>
/// (ou de uma <see cref="ValueObjects.ClausulaDnf"/>) contra os fatos de um candidato
/// (Story #916, ADR-0111). Um fato citado numa condição que não está resolvido — ausente,
/// <c>null</c>, ou de tipo incoerente com o operador — nunca é tratado como
/// <see cref="Falso"/>: o predicado avalia como <see cref="Indeterminado"/>, e quem consome o
/// veredicto decide o que fazer com a incerteza (nunca decide por omissão).
/// </summary>
/// <remarks>
/// <see cref="Indeterminado"/> é <c>0</c> — o zero-default de C# tem de ser o estado
/// fail-closed/mais conservador, nunca o mais permissivo. Um <see cref="Ternario"/> não
/// inicializado (ex.: um campo esquecido) nunca é silenciosamente lido como
/// <see cref="Verdadeiro"/>.
/// </remarks>
public enum Ternario
{
    Indeterminado = 0,
    Verdadeiro = 1,
    Falso = 2,
}
