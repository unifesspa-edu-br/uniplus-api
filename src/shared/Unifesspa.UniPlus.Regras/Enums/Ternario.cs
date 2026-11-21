namespace Unifesspa.UniPlus.Regras.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Resultado ternário fail-closed da avaliação de um <see cref="Regras.ValueObjects.PredicadoDnf"/>
/// (ou de uma <see cref="Regras.ValueObjects.ClausulaDnf"/>) contra os fatos de um candidato
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
[DescricaoDoContrato("Resultado da avaliação de uma condição: verdadeiro, falso ou indeterminado, que é estado próprio e não equivale a falso nem a ausência.")]
public enum Ternario
{
    [CodigoDeContrato("INDETERMINADO", "Indeterminado", Descricao = "Não há como decidir ainda: nunca é lido como verdadeiro nem como falso.")]
    Indeterminado = 0,

    [CodigoDeContrato("VERDADEIRO", "Verdadeiro")]
    Verdadeiro = 1,

    [CodigoDeContrato("FALSO", "Falso")]
    Falso = 2,
}
