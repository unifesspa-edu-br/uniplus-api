namespace Unifesspa.UniPlus.Regras.Formularios;

using Unifesspa.UniPlus.Kernel.Results;

internal sealed class UnicidadeDaColeta(
    IReadOnlySet<string> conhecidosAntes, IReadOnlyDictionary<string, IReadOnlyCollection<string>> derivacoes)
{
    private readonly HashSet<int> _ordens = [];
    private readonly HashSet<string> _codigosDeGrupo = new(StringComparer.Ordinal);
    internal Dictionary<string, ItemDoGrafo> Fatos { get; } = new(StringComparer.Ordinal);

    private DomainError? ConferirItens(IEnumerable<ItemDoGrafo> itens)
    {
        foreach (ItemDoGrafo item in itens)
        {
            if (ConferirItem(item) is { } recusa)
            {
                return recusa;
            }
        }
        return null;
    }

    private DomainError? ConferirItem(ItemDoGrafo item)
    {
        if (!Fatos.TryAdd(item.FatoCodigo, item))
        {
            return FatoDuplicado(item.FatoCodigo);
        }
        return _ordens.Add(item.Ordem) ? null : OrdemDuplicada(item.Ordem);
    }

    private DomainError? ConferirGrupos(IEnumerable<GrupoDoGrafo> grupos)
    {
        foreach (GrupoDoGrafo grupo in grupos)
        {
            if (ConferirGrupo(grupo) is { } recusa)
            {
                return recusa;
            }
        }

        return null;
    }

    private DomainError? ConferirGrupo(GrupoDoGrafo grupo)
    {
        if (Fatos.ContainsKey(grupo.Codigo) || derivacoes.ContainsKey(grupo.Codigo)
            || conhecidosAntes.Contains(grupo.Codigo) || !_codigosDeGrupo.Add(grupo.Codigo))
        {
            return FatoDuplicado(grupo.Codigo);
        }

        if (!_ordens.Add(grupo.Ordem))
        {
            return OrdemDuplicada(grupo.Ordem);
        }
        return ConferirSubitens(grupo);
    }

    private DomainError? ConferirSubitens(GrupoDoGrafo grupo)
    {
        HashSet<int> ordens = [];
        foreach (ItemDoGrafo subitem in grupo.Subitens)
        {
            if (ConferirSubitem(grupo, subitem, ordens) is { } recusa)
            {
                return recusa;
            }
        }

        return null;
    }

    private DomainError? ConferirSubitem(GrupoDoGrafo grupo, ItemDoGrafo subitem, HashSet<int> ordens)
    {
        if (_codigosDeGrupo.Contains(subitem.FatoCodigo) || derivacoes.ContainsKey(subitem.FatoCodigo)
            || !Fatos.TryAdd(subitem.FatoCodigo, subitem))
        {
            return FatoDuplicado(subitem.FatoCodigo);
        }
        return ordens.Add(subitem.Ordem) ? null : new DomainError(GrafoFormularioErrorCodes.OrdemDuplicada,
            $"A ordem {subitem.Ordem} é usada por mais de um campo do grupo '{grupo.Codigo}' — a ordem dentro do grupo precisa ser total.");
    }

    internal DomainError? Conferir(IReadOnlyList<ItemDoGrafo> itens, IReadOnlyList<GrupoDoGrafo> grupos) =>
        ConferirItens(itens) ?? ConferirGrupos(grupos);

    private static DomainError FatoDuplicado(string codigo) =>
        new(GrafoFormularioErrorCodes.FatoDuplicado, $"O código '{codigo}' aparece mais de uma vez no formulário, entre fatos e grupos.");

    private static DomainError OrdemDuplicada(int ordem) =>
        new(GrafoFormularioErrorCodes.OrdemDuplicada, $"A ordem {ordem} é usada por mais de um item ou grupo — a ordem de coleta precisa ser total.");
}
