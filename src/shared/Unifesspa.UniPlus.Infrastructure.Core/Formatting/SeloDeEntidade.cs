namespace Unifesspa.UniPlus.Infrastructure.Core.Formatting;

/// <summary>
/// Comparação de selo de entidade (<c>ETag</c>) para requisições condicionais.
/// </summary>
/// <remarks>
/// <para>
/// <c>If-None-Match</c> é avaliado pela função de comparação <b>fraca</b> (RFC 9110 §13.1.2): dois
/// selos coincidem quando os seus opacos são iguais, <b>independentemente</b> de um deles vir
/// marcado como fraco pelo prefixo <c>W/</c>. Comparar byte a byte parece mais estrito, mas na
/// prática desliga a revalidação: um proxy que comprime a resposta (nginx com <c>gzip</c>, por
/// exemplo) reescreve <c>"x"</c> como <c>W/"x"</c>, o cliente devolve o selo enfraquecido, o
/// servidor não o reconhece e responde o corpo inteiro em toda requisição — o oposto do que o selo
/// existe para fazer.
/// </para>
/// <para>
/// O opaco continua sendo comparado <b>ordinal</b>: ele não é texto localizável, e normalizá-lo
/// faria dois selos distintos passarem por iguais.
/// </para>
/// </remarks>
public static class SeloDeEntidade
{
    private const string PrefixoFraco = "W/";

    /// <summary>
    /// O cabeçalho <c>If-None-Match</c> recebido cobre <paramref name="seloAtual"/>? Aceita a lista
    /// separada por vírgula e o curinga <c>*</c>, que casa com qualquer representação existente.
    /// </summary>
    /// <param name="ifNoneMatch">Valor cru do cabeçalho, ou <see langword="null"/> quando ausente.</param>
    /// <param name="seloAtual">Selo corrente da representação, já entre aspas.</param>
    public static bool IfNoneMatchCoincide(string? ifNoneMatch, string seloAtual)
    {
        ArgumentNullException.ThrowIfNull(seloAtual);

        if (string.IsNullOrWhiteSpace(ifNoneMatch))
        {
            return false;
        }

        string opacoAtual = SemMarcaDeFraqueza(seloAtual);

        foreach (string candidato in ifNoneMatch.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (candidato == "*" || string.Equals(SemMarcaDeFraqueza(candidato), opacoAtual, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Descarta o prefixo <c>W/</c>, que marca o selo como fraco sem fazer parte do opaco. O
    /// prefixo é sensível a caixa por especificação — <c>w/</c> não é marca de fraqueza, é o começo
    /// de um selo malformado, e tratá-lo como marca aceitaria como iguais dois selos diferentes.
    /// </summary>
    private static string SemMarcaDeFraqueza(string selo) =>
        selo.StartsWith(PrefixoFraco, StringComparison.Ordinal) ? selo[PrefixoFraco.Length..] : selo;
}
