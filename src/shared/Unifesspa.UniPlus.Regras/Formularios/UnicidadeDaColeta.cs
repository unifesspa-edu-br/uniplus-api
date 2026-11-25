namespace Unifesspa.UniPlus.Regras.Formularios;

using Unifesspa.UniPlus.Kernel.Results;

internal static class UnicidadeDaColeta
{
    internal static DomainError? Conferir(
        IReadOnlyList<ItemDoGrafo> itens, IReadOnlyList<GrupoDoGrafo> grupos, IReadOnlySet<string> conhecidosAntes,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> derivacoes, out Dictionary<string, ItemDoGrafo> fatos)
    {
        EstadoDaUnicidade estado = new(conhecidosAntes, derivacoes);
        fatos = estado.Fatos;
        return estado.ConferirItens(itens) ?? estado.ConferirGrupos(grupos);
    }

    private sealed class EstadoDaUnicidade(
        IReadOnlySet<string> conhecidosAntes, IReadOnlyDictionary<string, IReadOnlyCollection<string>> derivacoes)
    {
        private readonly HashSet<int> _ordens = [];
        private readonly HashSet<string> _codigosDeGrupo = new(StringComparer.Ordinal);
        public Dictionary<string, ItemDoGrafo> Fatos { get; } = new(StringComparer.Ordinal);

        public DomainError? ConferirItens(IEnumerable<ItemDoGrafo> itens)
        {
            foreach (ItemDoGrafo item in itens)
            {
                if (!Fatos.TryAdd(item.FatoCodigo, item))
                {
                    return GrafoDoFormulario.FatoDuplicado(item.FatoCodigo);
                }
                if (!_ordens.Add(item.Ordem))
                {
                    return GrafoDoFormulario.OrdemDuplicada(item.Ordem);
                }
            }

            return null;
        }

        public DomainError? ConferirGrupos(IEnumerable<GrupoDoGrafo> grupos)
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
                return GrafoDoFormulario.FatoDuplicado(grupo.Codigo);
            }

            if (!_ordens.Add(grupo.Ordem))
            {
                return GrafoDoFormulario.OrdemDuplicada(grupo.Ordem);
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
                return GrafoDoFormulario.FatoDuplicado(subitem.FatoCodigo);
            }
            return ordens.Add(subitem.Ordem) ? null : new DomainError(GrafoFormularioErrorCodes.OrdemDuplicada,
                $"A ordem {subitem.Ordem} é usada por mais de um campo do grupo '{grupo.Codigo}' — a ordem dentro do grupo precisa ser total.");
        }
    }
}
