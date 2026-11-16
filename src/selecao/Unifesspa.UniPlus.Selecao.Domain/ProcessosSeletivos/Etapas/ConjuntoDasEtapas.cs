namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// As regras que dependem de mais de uma etapa: identificador e ordem únicos, a etapa de nota do ENEM e a composição da
/// média. Cada causa sai na posição da etapa que a viola, e uma regra que lê o valor de um dado recusado se suspende.
/// </summary>
internal static class ConjuntoDasEtapas
{
    internal static IReadOnlyList<FieldError> Recusas(IReadOnlyList<EtapaDeclarada> etapas) =>
    [
        .. IdsRepetidos(etapas),
        .. OrdensRepetidas(etapas),
        .. EtapaDoEnemDuplicada(etapas),
        .. EtapasDoEnemQueNaoComponhamANota(etapas),
        .. EtapasDoEnemComBanca(etapas),
        .. EtapasDoEnemComProdutoOuRecursoEmAto(etapas),
        .. NenhumaEtapaComponeNota(etapas),
    ];

    internal static DomainError OrdemDuplicada() => new(
        "ProcessoSeletivo.OrdemEtapaDuplicada",
        "Cada etapa deve ter uma ordem única dentro do processo.");

    internal static DomainError NenhumaEtapaComponeNota() => new(
        "ProcessoSeletivo.NenhumaEtapaComponeNota",
        "Ao menos uma etapa deve ter caráter classificatória ou ambas, com peso, para compor a nota final.");

    internal static DomainError NotaDoEnemDuplicada(IReadOnlyList<string> nomes) => new(
        "ProcessoSeletivo.EtapaNotaEnemDuplicada",
        $"O processo admite uma única etapa de nota do ENEM, e declara {nomes.Count}: {string.Join(", ", nomes.Select(static nome => $"\"{nome}\""))}.");

    internal static DomainError NotaDoEnemNaoCompoeNota(string nome) => new(
        "ProcessoSeletivo.EtapaNotaEnemNaoCompoeNota",
        $"A etapa \"{nome}\" tem a nota vinda do ENEM e precisa compor a média: declare caráter classificatória ou ambas e um peso.");

    internal static DomainError NotaDoEnemComBanca(string nome) => new(
        "ProcessoSeletivo.EtapaNotaEnemComBanca",
        $"A etapa \"{nome}\" tem a nota vinda do ENEM e não admite banca: ninguém atribui a nota, e quem julga o recurso é declarado na fase de recursos.");

    internal static DomainError NotaDoEnemComProdutoOuRecursoEmAto(string nome) => new(
        "ProcessoSeletivo.EtapaNotaEnemComProdutoOuRecursoEmAto",
        $"A etapa \"{nome}\" tem a nota vinda do ENEM e não declara produto nem recurso ancorado em ato: o resultado é publicado pela fase, e o recurso coletivo sobre a lista geral é o da regra de recurso da fase. Na etapa cabe só o recurso individual, ancorado na ciência do candidato.");

    private static IEnumerable<FieldError> IdsRepetidos(IReadOnlyList<EtapaDeclarada> etapas) =>
        EtapasDoProcesso.PosicoesComIdRepetido(etapas.Select(static etapa => (Guid?)etapa.Id))
            .Select(posicao => EtapasDoProcesso.NaEtapa(posicao, "id", EtapasDoProcesso.IdRepetido()));

    private static IEnumerable<FieldError> OrdensRepetidas(IReadOnlyList<EtapaDeclarada> etapas)
    {
        HashSet<int> vistas = [];
        for (int posicao = 0; posicao < etapas.Count; posicao++)
        {
            if (etapas[posicao].Ordem is { } ordem && !etapas[posicao].Recusado(DadoDaEtapa.Ordem) && !vistas.Add(ordem))
            {
                yield return EtapasDoProcesso.NaEtapa(posicao, "ordem", OrdemDuplicada());
            }
        }
    }

    private static IEnumerable<(int Posicao, EtapaDeclarada Etapa)> DoEnem(IReadOnlyList<EtapaDeclarada> etapas) =>
        etapas.Select(static (etapa, posicao) => (posicao, etapa)).Where(static par => par.etapa.NotaVemDoEnem);

    private static IEnumerable<FieldError> EtapaDoEnemDuplicada(IReadOnlyList<EtapaDeclarada> etapas)
    {
        (int Posicao, EtapaDeclarada Etapa)[] doEnem = [.. DoEnem(etapas)];

        return doEnem.Length > 1
            ? [EtapasDoProcesso.NaEtapa(doEnem[1].Posicao, null, NotaDoEnemDuplicada([.. doEnem.Select(static par => par.Etapa.Nome)]))]
            : [];
    }

    private static IEnumerable<FieldError> EtapasDoEnemQueNaoComponhamANota(IReadOnlyList<EtapaDeclarada> etapas) =>
        DoEnem(etapas)
            .Where(static par => par.Etapa.ComposicaoDaMediaDeterminada && !par.Etapa.ComponeNota)
            .Select(static par => EtapasDoProcesso.NaEtapa(par.Posicao, "peso", NotaDoEnemNaoCompoeNota(par.Etapa.Nome)));

    private static IEnumerable<FieldError> EtapasDoEnemComBanca(IReadOnlyList<EtapaDeclarada> etapas) =>
        DoEnem(etapas)
            .Where(static par => par.Etapa.Bancas.TemBancas)
            .Select(static par => EtapasDoProcesso.NaEtapa(par.Posicao, "bancas", NotaDoEnemComBanca(par.Etapa.Nome)));

    private static IEnumerable<FieldError> EtapasDoEnemComProdutoOuRecursoEmAto(IReadOnlyList<EtapaDeclarada> etapas) =>
        DoEnem(etapas)
            .Where(static par => par.Etapa.Atos.TemProdutos || par.Etapa.Atos.TemRecursoEmAto)
            .Select(static par => EtapasDoProcesso.NaEtapa(par.Posicao, "produtos", NotaDoEnemComProdutoOuRecursoEmAto(par.Etapa.Nome)));

    // Sem etapa que componha a nota o divisor da média é zero. A regra lê o caráter e o peso de todas as etapas, então se
    // suspende quando algum foi recusado; e já foi dita pela etapa do ENEM que não compõe, que é a causa real.
    private static IEnumerable<FieldError> NenhumaEtapaComponeNota(IReadOnlyList<EtapaDeclarada> etapas) =>
        etapas.Count > 0
        && etapas.All(static etapa => etapa.ComposicaoDaMediaDeterminada && !etapa.ComponeNota)
        && !EtapasDoEnemQueNaoComponhamANota(etapas).Any()
            ? [new FieldError("etapas", NenhumaEtapaComponeNota())]
            : [];
}
