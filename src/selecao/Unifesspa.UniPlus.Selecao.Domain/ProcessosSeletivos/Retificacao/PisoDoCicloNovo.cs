namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Retificacao;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Os resultados preliminares publicados cujo ciclo a retificação precisa refazer, calculados do documento: o
/// preliminar entra quando o lugar dele (a fase ou a etapa que o declara) mudou o que entra no preliminar, quando a
/// fase mudou de momento (ganhou ou perdeu fase anterior, pela marca "no mesmo momento da anterior" ou pela ordem), ou quando
/// mudou uma fase que vem antes dela, na versão publicada ou na nova, porque o que vem antes alimenta o que vem depois.
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
        HashSet<Guid> mantidas = [.. fasesDaBase.Select(static fase => fase.FaseCanonicaOrigemId).Where(novasPorOrigem.ContainsKey)];
        Dictionary<Guid, HashSet<Guid>> antesNaBase = MantidasAntesDeCada(fasesDaBase, mantidas);
        Dictionary<Guid, HashSet<Guid>> antesNasNovas = MantidasAntesDeCada(fasesNovas, mantidas);
        HashSet<Guid> alteradas = FasesAlteradas(fasesDaBase, novasPorOrigem, antesNaBase, antesNasNovas);

        return
        [
            .. mantidas.Where(origem => alteradas.Contains(origem)
                || alteradas.Any(alterada => antesNaBase[origem].Contains(alterada) || antesNasNovas[origem].Contains(alterada))),
        ];
    }

    // Alterada é a fase mantida cuja entrada do preliminar, marca ou momento mudou, ainda que nenhum campo dela tenha sido editado.
    private static HashSet<Guid> FasesAlteradas(
        IReadOnlyCollection<FaseCronograma> fasesDaBase,
        Dictionary<Guid, FaseCronograma> novasPorOrigem,
        Dictionary<Guid, HashSet<Guid>> antesNaBase,
        Dictionary<Guid, HashSet<Guid>> antesNasNovas) =>
    [
        .. fasesDaBase
            .Where(fase => novasPorOrigem.TryGetValue(fase.FaseCanonicaOrigemId, out FaseCronograma? nova)
                && (EntradaOuMarcaMudou(fase, nova) || !antesNaBase[fase.FaseCanonicaOrigemId].SetEquals(antesNasNovas[fase.FaseCanonicaOrigemId])))
            .Select(static fase => fase.FaseCanonicaOrigemId),
    ];

    private static bool EntradaOuMarcaMudou(FaseCronograma daBase, FaseCronograma nova) =>
        !daBase.EntradaDoPreliminar().Equals(nova.EntradaDoPreliminar()) || daBase.NoMesmoMomentoDaAnterior != nova.NoMesmoMomentoDaAnterior;

    // O momento da fase mantida é o conjunto das fases mantidas que vêm antes dela na ordem da versão: o número do momento muda
    // em todas só por entrar uma fase no começo, e o conjunto muda só nas que ganham ou perdem uma anterior.
    private static Dictionary<Guid, HashSet<Guid>> MantidasAntesDeCada(IReadOnlyCollection<FaseCronograma> fasesDaVersao, HashSet<Guid> mantidas)
    {
        OrdemDoCronograma ordem = OrdemDoCronograma.Do(fasesDaVersao);
        FaseCronograma[] daVersao = [.. fasesDaVersao.Where(fase => mantidas.Contains(fase.FaseCanonicaOrigemId))];

        return daVersao.ToDictionary(
            static fase => fase.FaseCanonicaOrigemId,
            fase => daVersao.Where(outra => ordem.Posterior(fase, outra)).Select(static outra => outra.FaseCanonicaOrigemId).ToHashSet());
    }

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
