namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;

using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Apura o valor de um conceito categórico a partir das atribuições dos corretores. O critério e o número
/// de corretores exigidos vêm da regra de consolidação escolhida pelo edital; esta apuração não fixa
/// nenhum. Recebe uma atribuição vigente por corretor distinto, e a ordem delas não importa.
/// </summary>
/// <remarks>
/// Quando chegam mais atribuições que as exigidas, a apuração conta todas. A apuração só identifica o
/// estado; o tratamento do empate e da falta de unanimidade é parâmetro da regra escolhida.
/// </remarks>
public static class ConsolidacaoDoConceitoCategorico
{
    /// <summary>O resultado da apuração das atribuições pelo critério da regra, com os corretores que ela exige.</summary>
    /// <exception cref="ArgumentException">
    /// O conceito não é categórico ou uma atribuição não é valor do conceito: quem chama já conferiu o
    /// lançamento, e isso é defeito de quem chama.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">Os corretores exigidos são menos que um, ou o critério é desconhecido.</exception>
    public static ResultadoDaConsolidacaoDoConceito Consolidar(
        ConceitoDaEtapa conceitoCategorico,
        CriterioDeConsolidacaoDoConceito criterio,
        int corretoresExigidos,
        IReadOnlyList<string> valoresAtribuidos)
    {
        ExigirPrecondicoes(conceitoCategorico, criterio, corretoresExigidos, valoresAtribuidos);

        return valoresAtribuidos.Count < corretoresExigidos
            ? ResultadoDaConsolidacaoDoConceito.AguardandoCorrecao
            : Apurar(criterio, valoresAtribuidos);
    }

    private static void ExigirPrecondicoes(
        ConceitoDaEtapa conceito, CriterioDeConsolidacaoDoConceito criterio, int corretoresExigidos, IReadOnlyList<string> valoresAtribuidos)
    {
        ArgumentNullException.ThrowIfNull(conceito);
        ArgumentNullException.ThrowIfNull(valoresAtribuidos);
        ArgumentOutOfRangeException.ThrowIfLessThan(corretoresExigidos, 1);
        if (!Enum.IsDefined(criterio))
        {
            throw new ArgumentOutOfRangeException(nameof(criterio), criterio, "Critério de consolidação desconhecido.");
        }

        ExigirValoresDoConceito(conceito, valoresAtribuidos);
    }

    private static void ExigirValoresDoConceito(ConceitoDaEtapa conceito, IReadOnlyList<string> valoresAtribuidos)
    {
        if (conceito.Natureza != NaturezaConceito.Categorico)
        {
            throw new ArgumentException("A consolidação por valor exige um conceito categórico.", nameof(conceito));
        }

        if (valoresAtribuidos.Any(valor => conceito.ValorDeCodigo(valor) is null))
        {
            throw new ArgumentException("Toda atribuição deve ser um valor do conceito da etapa.", nameof(valoresAtribuidos));
        }
    }

    private static ResultadoDaConsolidacaoDoConceito Apurar(CriterioDeConsolidacaoDoConceito criterio, IReadOnlyList<string> valores)
    {
        List<KeyValuePair<string, int>> contagens =
            [.. valores.CountBy(static valor => valor, StringComparer.Ordinal).OrderByDescending(static contagem => contagem.Value)];

        return criterio switch
        {
            CriterioDeConsolidacaoDoConceito.Unanimidade => PorUnanimidade(contagens),
            CriterioDeConsolidacaoDoConceito.MaioriaSimples => PorMaioriaSimples(contagens),
            CriterioDeConsolidacaoDoConceito.MaioriaAbsoluta => PorMaioriaAbsoluta(contagens, valores.Count),
            _ => throw new ArgumentOutOfRangeException(nameof(criterio), criterio, "Critério de consolidação desconhecido."),
        };
    }

    private static ResultadoDaConsolidacaoDoConceito PorUnanimidade(List<KeyValuePair<string, int>> contagens) =>
        contagens.Count == 1
            ? ResultadoDaConsolidacaoDoConceito.Consolidado(contagens[0].Key)
            : ResultadoDaConsolidacaoDoConceito.SemUnanimidade;

    private static ResultadoDaConsolidacaoDoConceito PorMaioriaSimples(List<KeyValuePair<string, int>> contagens) =>
        contagens.Count == 1 || contagens[0].Value > contagens[1].Value
            ? ResultadoDaConsolidacaoDoConceito.Consolidado(contagens[0].Key)
            : ResultadoDaConsolidacaoDoConceito.Empate;

    private static ResultadoDaConsolidacaoDoConceito PorMaioriaAbsoluta(List<KeyValuePair<string, int>> contagens, int total) =>
        contagens[0].Value * 2 > total
            ? ResultadoDaConsolidacaoDoConceito.Consolidado(contagens[0].Key)
            : ResultadoDaConsolidacaoDoConceito.Empate;
}
