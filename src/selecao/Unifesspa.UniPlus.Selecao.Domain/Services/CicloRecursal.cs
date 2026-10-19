namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

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

    /// <summary>
    /// Os tipos de ato declarados como definitivo em mais de um lugar do cronograma (fase ou
    /// etapa), cada um com os lugares que o declaram, na ordem em que o cronograma os cita.
    /// </summary>
    /// <remarks>
    /// Conta declarações, não ciclos: um único definitivo da fase concluinte que fecha dois
    /// ciclos é uma declaração e não entra. O definitivo declarado em dois lugares tem dois
    /// fechamentos, e o edital precisa confirmar que é isso que quer.
    /// </remarks>
    public static IReadOnlyList<(string AtoCodigo, IReadOnlyList<string> Lugares)> DefinitivosEmMaisDeUmLugar(
        IEnumerable<DeclaracaoDeAto> declaracoes)
    {
        ArgumentNullException.ThrowIfNull(declaracoes);

        return
        [
            .. declaracoes
                .Where(static d => d.Papel == PapelProdutoFase.Definitivo)
                .GroupBy(static d => d.AtoCodigo, StringComparer.Ordinal)
                .Select(static grupo => (
                    AtoCodigo: grupo.Key,
                    Lugares: (IReadOnlyList<string>)[.. grupo.Select(static d => d.Lugar).Distinct(StringComparer.Ordinal)]))
                .Where(static g => g.Lugares.Count > 1),
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
