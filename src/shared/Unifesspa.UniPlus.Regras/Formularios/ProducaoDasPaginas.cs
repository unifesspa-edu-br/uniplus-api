namespace Unifesspa.UniPlus.Regras.Formularios;

using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// O que um conjunto de páginas da definição produz: os grupos que elas perguntam, os fatos que elas
/// perguntam em item ou subitem, e — pelo <see cref="Fechamento"/> — o que se calcula disso pelas
/// derivações e pelos agregados sobre os grupos delas. O agregado sobre grupo de fora das páginas não se
/// calcula com elas.
/// </summary>
public sealed class ProducaoDasPaginas
{
    private readonly IReadOnlyDictionary<string, DerivacaoDoFato> _derivacoes;
    private readonly IReadOnlyDictionary<string, DefinicaoAgregado> _agregados;

    private ProducaoDasPaginas(
        IReadOnlySet<string> grupos,
        IReadOnlySet<string> produzidos,
        IReadOnlyDictionary<string, DerivacaoDoFato> derivacoes,
        IReadOnlyDictionary<string, DefinicaoAgregado> agregados)
    {
        Grupos = grupos;
        Produzidos = produzidos;
        _derivacoes = derivacoes;
        _agregados = agregados;
    }

    /// <summary>Os grupos repetíveis que as páginas perguntam.</summary>
    public IReadOnlySet<string> Grupos { get; }

    /// <summary>Os fatos que as páginas perguntam, em item ou em subitem de grupo.</summary>
    public IReadOnlySet<string> Produzidos { get; }

    public static ProducaoDasPaginas De(DefinicaoFormulario definicao, IEnumerable<DefinicaoPagina> paginas)
    {
        ArgumentNullException.ThrowIfNull(definicao);
        ArgumentNullException.ThrowIfNull(paginas);

        DefinicaoPagina[] daProducao = [.. paginas];
        HashSet<string> grupos = new(daProducao.SelectMany(static e => e.Grupos).Select(static g => g.Codigo), StringComparer.Ordinal);
        HashSet<string> produzidos = new(
            daProducao.SelectMany(static e => e.Itens.Select(static i => i.FatoCodigo)
                .Concat(e.Grupos.SelectMany(static g => g.Subitens).Select(static s => s.FatoCodigo))),
            StringComparer.Ordinal);
        return new ProducaoDasPaginas(
            grupos,
            produzidos,
            definicao.Derivacoes.ToDictionary(static d => d.CodigoFato, StringComparer.Ordinal),
            definicao.Agregados.Where(a => grupos.Contains(a.GrupoCodigo)).ToDictionary(static a => a.Codigo, StringComparer.Ordinal));
    }

    /// <summary>
    /// O que os fatos citados alcançam sobre o que as páginas produzem: as derivações e os agregados
    /// atravessados e, como pressupostos, o que as páginas não produzem nem calculam.
    /// </summary>
    public FechamentoDasDependencias Fechamento(IEnumerable<string> citados) =>
        FechamentoDasDependencias.De(citados, Produzidos, _derivacoes, _agregados);
}
