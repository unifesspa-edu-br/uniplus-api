namespace Unifesspa.UniPlus.Regras.Formularios;

using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// O que um conjunto de etapas da definição produz: os grupos que elas perguntam, os fatos que elas
/// perguntam em item ou subitem, e — pelo <see cref="Fechamento"/> — o que se calcula disso pelas
/// derivações e pelos agregados sobre os grupos delas. O agregado sobre grupo de fora das etapas não se
/// calcula com elas.
/// </summary>
public sealed class ProducaoDasEtapas
{
    private readonly IReadOnlyDictionary<string, RegrasDerivacaoFato> _derivacoes;
    private readonly IReadOnlyDictionary<string, DefinicaoAgregado> _agregados;

    private ProducaoDasEtapas(
        IReadOnlySet<string> grupos,
        IReadOnlySet<string> produzidos,
        IReadOnlyDictionary<string, RegrasDerivacaoFato> derivacoes,
        IReadOnlyDictionary<string, DefinicaoAgregado> agregados)
    {
        Grupos = grupos;
        Produzidos = produzidos;
        _derivacoes = derivacoes;
        _agregados = agregados;
    }

    /// <summary>Os grupos repetíveis que as etapas perguntam.</summary>
    public IReadOnlySet<string> Grupos { get; }

    /// <summary>Os fatos que as etapas perguntam, em item ou em subitem de grupo.</summary>
    public IReadOnlySet<string> Produzidos { get; }

    public static ProducaoDasEtapas De(DefinicaoFormulario definicao, IEnumerable<DefinicaoEtapa> etapas)
    {
        ArgumentNullException.ThrowIfNull(definicao);
        ArgumentNullException.ThrowIfNull(etapas);

        DefinicaoEtapa[] daProducao = [.. etapas];
        HashSet<string> grupos = new(daProducao.SelectMany(static e => e.Grupos).Select(static g => g.Codigo), StringComparer.Ordinal);
        HashSet<string> produzidos = new(
            daProducao.SelectMany(static e => e.Itens.Select(static i => i.FatoCodigo)
                .Concat(e.Grupos.SelectMany(static g => g.Subitens).Select(static s => s.FatoCodigo))),
            StringComparer.Ordinal);
        return new ProducaoDasEtapas(
            grupos,
            produzidos,
            definicao.Derivacoes.ToDictionary(static d => d.CodigoFato, StringComparer.Ordinal),
            definicao.Agregados.Where(a => grupos.Contains(a.GrupoCodigo)).ToDictionary(static a => a.Codigo, StringComparer.Ordinal));
    }

    /// <summary>
    /// O que os fatos citados alcançam sobre o que as etapas produzem: as derivações e os agregados
    /// atravessados e, como pressupostos, o que as etapas não produzem nem calculam.
    /// </summary>
    public FechamentoDasDependencias Fechamento(IEnumerable<string> citados) =>
        FechamentoDasDependencias.De(citados, Produzidos, _derivacoes, _agregados);
}
