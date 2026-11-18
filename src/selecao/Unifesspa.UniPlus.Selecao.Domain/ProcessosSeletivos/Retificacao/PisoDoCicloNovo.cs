namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Retificacao;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Os resultados preliminares publicados cujo ciclo a retificação precisa refazer, calculados do documento: o
/// preliminar entra quando o lugar dele (a fase ou a etapa que o declara) mudou o que entra no preliminar, ou
/// quando mudou uma fase de momento anterior ao da fase dele, porque o que vem antes alimenta o que vem depois.
/// Para a etapa, a fase onde ela acontece também conta. O lugar que sai do cronograma novo não conta aqui,
/// porque a saída do item é recusa do ciclo recursal aberto. O sistema não sabe o que é dado de entrada de cada
/// tipo de ato: este é o piso, e o bloco da retificação pode declarar mais.
/// </summary>
internal static class PisoDoCicloNovo
{
    internal static IEnumerable<IdentidadeDoItemDoEdital> Preliminares(
        IReadOnlyCollection<FaseCronograma> fasesDaBase,
        IReadOnlyCollection<EtapaProcesso> etapasDaBase,
        IReadOnlyCollection<FaseCronograma> fasesNovas,
        IReadOnlyCollection<EtapaProcesso> etapasNovas,
        IReadOnlySet<IdentidadeDoItemDoEdital> preliminaresAbertos)
    {
        HashSet<Guid> fasesAtingidas = FasesAtingidas(fasesDaBase, fasesNovas);
        HashSet<Guid> etapasAtingidas = EtapasAtingidas(fasesDaBase, etapasDaBase, etapasNovas, fasesAtingidas);
        IEnumerable<IdentidadeDoItemDoEdital> produtosAtingidos =
        [
            .. fasesDaBase.Where(fase => fasesAtingidas.Contains(fase.FaseCanonicaOrigemId))
                .SelectMany(static fase => fase.Produtos.Select(produto => IdentidadeDoItemDoEdital.DaFase(fase, produto))),
            .. etapasDaBase.Where(etapa => etapasAtingidas.Contains(etapa.Id))
                .SelectMany(static etapa => etapa.Produtos.Select(produto => IdentidadeDoItemDoEdital.DaEtapa(etapa, produto))),
        ];

        return produtosAtingidos.Where(preliminaresAbertos.Contains);
    }

    private static HashSet<Guid> FasesAtingidas(IReadOnlyCollection<FaseCronograma> fasesDaBase, IReadOnlyCollection<FaseCronograma> fasesNovas)
    {
        Dictionary<Guid, FaseCronograma> novasPorOrigem = fasesNovas.ToDictionary(static fase => fase.FaseCanonicaOrigemId);
        HashSet<Guid> alteradas = FasesAlteradas(fasesDaBase, novasPorOrigem);
        Dictionary<Guid, int> momentos = fasesDaBase.ToDictionary(static fase => fase.FaseCanonicaOrigemId, fase => MomentoDaFase.Da(fase, fasesDaBase));

        return
        [
            .. momentos.Keys.Where(origem => novasPorOrigem.ContainsKey(origem)
                && (alteradas.Contains(origem) || alteradas.Any(outra => momentos[outra] < momentos[origem]))),
        ];
    }

    private static HashSet<Guid> FasesAlteradas(IReadOnlyCollection<FaseCronograma> fasesDaBase, Dictionary<Guid, FaseCronograma> novasPorOrigem) =>
    [
        .. fasesDaBase
            .Where(fase => novasPorOrigem.TryGetValue(fase.FaseCanonicaOrigemId, out FaseCronograma? nova)
                && !fase.EntradaDoPreliminar().Equals(nova.EntradaDoPreliminar()))
            .Select(static fase => fase.FaseCanonicaOrigemId),
    ];

    private static HashSet<Guid> EtapasAtingidas(
        IReadOnlyCollection<FaseCronograma> fasesDaBase,
        IReadOnlyCollection<EtapaProcesso> etapasDaBase,
        IReadOnlyCollection<EtapaProcesso> etapasNovas,
        HashSet<Guid> fasesAtingidas)
    {
        Dictionary<Guid, EtapaProcesso> novasPorId = etapasNovas.ToDictionary(static etapa => etapa.Id);

        return
        [
            .. etapasDaBase
                .Where(etapa => novasPorId.TryGetValue(etapa.Id, out EtapaProcesso? nova)
                    && (!etapa.EntradaDoPreliminar().Equals(nova.EntradaDoPreliminar()) || EstaNaFaseAtingida(etapa, fasesDaBase, fasesAtingidas)))
                .Select(static etapa => etapa.Id),
        ];
    }

    private static bool EstaNaFaseAtingida(EtapaProcesso etapa, IReadOnlyCollection<FaseCronograma> fasesDaBase, HashSet<Guid> fasesAtingidas) =>
        fasesDaBase.FirstOrDefault(fase => string.Equals(fase.Codigo, etapa.FaseCodigo, StringComparison.Ordinal)) is { } fase
        && fasesAtingidas.Contains(fase.FaseCanonicaOrigemId);
}
