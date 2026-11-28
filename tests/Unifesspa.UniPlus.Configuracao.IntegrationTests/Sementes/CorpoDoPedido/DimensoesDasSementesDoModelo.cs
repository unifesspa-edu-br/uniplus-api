namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Sementes.CorpoDoPedido;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Regras.Entradas;

/// <summary>
/// O maior de cada quantidade que os modelos semeados têm, medido sobre o conteúdo que a leitura devolve e a escrita
/// recebe. O pior caso sintético repete cada quantidade em todo o modelo.
/// </summary>
internal sealed record DimensoesDasSementesDoModelo(
    int Paginas, int Termos, int Grupos, int Subitens, int Pressupostos, int Clausulas, int Condicoes, int Restricoes, int Opcoes, int ValoresPorOpcao)
{
    public static DimensoesDasSementesDoModelo De(IReadOnlyCollection<ConteudoDoModeloInput> modelos) => new(
        Maior(modelos, static m => m.Paginas?.Count),
        Maior(modelos, static m => m.Termos?.Count),
        Maior(modelos, static m => m.Grupos?.Count),
        Maior(modelos, static m => m.Grupos?.Select(static g => g.Subitens!.Count).DefaultIfEmpty(0).Max()),
        Maior(modelos, static m => m.Pressupostos?.Count),
        Maior(Predicados(modelos), static p => p.Count),
        Maior(Predicados(modelos), static p => p.Select(static c => c.Count).DefaultIfEmpty(0).Max()),
        Maior(TodosOsItens(modelos), static i => i.Regras?.Restricoes?.Count),
        Maior(RestricoesDeValor(modelos), static r => r.Entradas?.Count),
        Maior(RestricoesDeValor(modelos), static r => r.Entradas?.Select(static e => e.Valores.Count).DefaultIfEmpty(0).Max()));

    private static int Maior<T>(IEnumerable<T> origem, Func<T, int?> medida) =>
        origem.Select(medida).Max() ?? 0;

    private static IEnumerable<FatoColetadoInput> TodosOsItens(IEnumerable<ConteudoDoModeloInput> modelos) =>
        modelos.SelectMany(static m => (m.Itens ?? []).Concat((m.Grupos ?? []).SelectMany(static g => g.Subitens ?? [])));

    private static IEnumerable<RestricaoValorInput> RestricoesDeValor(IEnumerable<ConteudoDoModeloInput> modelos) =>
        TodosOsItens(modelos).SelectMany(static i => i.Regras?.Restricoes ?? []);

    private static IEnumerable<IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>> Predicados(IEnumerable<ConteudoDoModeloInput> modelos) =>
    [
        .. modelos.SelectMany(static m => (m.Paginas ?? []).Select(static p => p.Exibicao)).OfType<IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>>(),
        .. TodosOsItens(modelos).SelectMany(static i => i.Regras?.Predicados() ?? []).OfType<IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>>(),
    ];
}
