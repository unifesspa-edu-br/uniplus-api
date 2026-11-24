namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Regras.Services;

/// <summary>Ordena as partes e dependências de uma montagem sem ciclo.</summary>
internal sealed class OrdemDasDependencias
{
    private readonly MontagemDasDependencias _montagem;
    private readonly ILookup<ParteDaConfiguracao, ParteDaConfiguracao> _dependentes;
    private readonly Dictionary<ParteDaConfiguracao, long> _posicoes = [];

    internal OrdemDasDependencias(MontagemDasDependencias montagem)
    {
        _montagem = montagem;
        _dependentes = montagem.Dependencias.ToLookup(static d => d.Origem, static d => d.Destino);
    }

    internal IReadOnlyList<ParteDaConfiguracao> PartesEmOrdem() => [.. _montagem.Partes.Order(Comparer<ParteDaConfiguracao>.Create(CompararPartes))];
    internal IReadOnlyList<DependenciaEntrePartes> DependenciasEmOrdem() =>
        [.. _montagem.Dependencias.Order(Comparer<DependenciaEntrePartes>.Create(CompararDependencias))];

    internal IReadOnlyList<ParteDaConfiguracao> OrdemDeColeta()
    {
        IReadOnlyList<ParteDaConfiguracao> partes = PartesEmOrdem();
        ILookup<ParteDaConfiguracao, ParteDaConfiguracao> citacoes = _montagem.Dependencias.ToLookup(static d => d.Destino, static d => d.Origem);
        Dictionary<string, ParteDaConfiguracao> porIdentidade = partes.ToDictionary(static p => p.Identidade, StringComparer.Ordinal);
        List<NoDoGrafo> nos = [.. partes.Select((p, indice) => new NoDoGrafo(p.Identidade, [.. citacoes[p].Select(static c => c.Identidade)], indice))];
        return [.. GrafoDeFatos.Ordenar(nos).Ordem.Select(id => porIdentidade[id])];
    }

    private long PosicaoEfetiva(ParteDaConfiguracao parte)
    {
        if (_posicoes.TryGetValue(parte, out long posicao))
        {
            return posicao;
        }

        posicao = _montagem.PosicoesDeclaradas.GetValueOrDefault(parte, long.MaxValue);
        foreach (ParteDaConfiguracao dependente in _dependentes[parte])
        {
            posicao = Math.Min(posicao, PosicaoEfetiva(dependente));
        }

        _posicoes[parte] = posicao;
        return posicao;
    }

    private int CompararPartes(ParteDaConfiguracao? primeira, ParteDaConfiguracao? segunda)
    {
        int porPosicao = PosicaoEfetiva(primeira!).CompareTo(PosicaoEfetiva(segunda!));
        return porPosicao != 0 ? porPosicao : ParteDaConfiguracao.PorTipoECodigo.Compare(primeira, segunda);
    }

    private int CompararDependencias(DependenciaEntrePartes? primeira, DependenciaEntrePartes? segunda)
    {
        int porMotivo = ((int)primeira!.Motivo).CompareTo((int)segunda!.Motivo);
        if (porMotivo != 0)
        {
            return porMotivo;
        }

        int porOrigem = CompararPartes(primeira.Origem, segunda.Origem);
        return porOrigem != 0 ? porOrigem : CompararPartes(primeira.Destino, segunda.Destino);
    }
}
