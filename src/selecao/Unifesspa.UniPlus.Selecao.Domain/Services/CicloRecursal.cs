namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// O único ponto que diz, dentro de uma fase ou de uma etapa, quais matérias ficam com o
/// ciclo recursal aberto. A matéria é o tipo de ato; o papel diz se a publicação é preliminar
/// ou definitiva.
/// </summary>
/// <remarks>
/// A fase e a etapa declaram produtos de tipos diferentes, e a regra de pareamento é uma só:
/// o preliminar de um tipo é fechado pelo definitivo do mesmo tipo no mesmo lugar. Os gates
/// do cronograma e da gravação das etapas perguntam aqui, e nenhum deles reescreve o
/// pareamento.
/// </remarks>
public static class CicloRecursal
{
    /// <summary>
    /// Os tipos de ato cujo preliminar é publicado sem o definitivo do mesmo tipo no mesmo
    /// lugar: o ciclo desses tipos fica para outra fase fechar, ou nunca fecha.
    /// </summary>
    public static IReadOnlyList<string> TiposComCicloAberto(IEnumerable<(string AtoCodigo, PapelProdutoFase? Papel)> produtos)
    {
        ArgumentNullException.ThrowIfNull(produtos);

        List<(string AtoCodigo, PapelProdutoFase? Papel)> declarados = [.. produtos];
        HashSet<string> definitivos = [.. TiposComPapel(declarados, PapelProdutoFase.Definitivo)];

        return [.. TiposComPapel(declarados, PapelProdutoFase.Preliminar).Where(tipo => !definitivos.Contains(tipo))];
    }

    /// <summary>
    /// Os tipos de ato que o lugar fecha sozinho: o preliminar e o definitivo do mesmo tipo
    /// são publicados nele.
    /// </summary>
    public static IReadOnlyList<string> TiposFechadosNoProprioLugar(IEnumerable<(string AtoCodigo, PapelProdutoFase? Papel)> produtos)
    {
        ArgumentNullException.ThrowIfNull(produtos);

        List<(string AtoCodigo, PapelProdutoFase? Papel)> declarados = [.. produtos];
        HashSet<string> definitivos = [.. TiposComPapel(declarados, PapelProdutoFase.Definitivo)];

        return [.. TiposComPapel(declarados, PapelProdutoFase.Preliminar).Where(definitivos.Contains)];
    }

    /// <summary>
    /// Os identificadores dos produtos preliminares do tipo <paramref name="atoCodigo"/> que o
    /// definitivo do mesmo tipo fecha neste lugar, a fase ou a etapa que os declara.
    /// </summary>
    public static IReadOnlyList<Guid> PreliminaresDoTipo(
        IEnumerable<(Guid Id, string AtoCodigo, PapelProdutoFase? Papel)> produtos,
        string atoCodigo)
    {
        ArgumentNullException.ThrowIfNull(produtos);

        return
        [
            .. produtos
                .Where(p => p.Papel == PapelProdutoFase.Preliminar && string.Equals(p.AtoCodigo, atoCodigo, StringComparison.Ordinal))
                .Select(static p => p.Id),
        ];
    }

    private static IEnumerable<string> TiposComPapel(
        IEnumerable<(string AtoCodigo, PapelProdutoFase? Papel)> produtos,
        PapelProdutoFase papel) =>
        produtos
            .Where(p => p.Papel == papel)
            .Select(static p => p.AtoCodigo)
            .Distinct(StringComparer.Ordinal);
}
