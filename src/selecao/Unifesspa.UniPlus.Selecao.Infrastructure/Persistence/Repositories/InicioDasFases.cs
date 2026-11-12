namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

/// <summary>
/// Quais fases começaram, lendo cada fato do processo na versão publicada que vigia quando ele aconteceu. A janela só
/// vale como início se abriu enquanto a versão que a declarava vigia; o ato de uma etapa, a avaliação lançada e a
/// inscrição recebida contam para a fase a que a etapa, ou a coleta, pertencia na versão vigente no instante do fato
/// (UNI-REQ-0333). O que começou não "desinicia": a retificação que adia a janela ou muda a etapa de fase não apaga o início.
/// </summary>
internal static class InicioDasFases
{
    /// <summary>Uma versão publicada já vigente: as fases que ela declara e o instante em que a sucessora passou a vigir.</summary>
    internal sealed record VersaoDasFases(DateTimeOffset VigenteAPartirDe, DateTimeOffset? SubstituidaEm, IReadOnlyList<FasesNoEnvelope.Fase> Fases);

    /// <summary>Os fatos do processo com o instante em que aconteceram; o ato e a inscrição são os fatos de uma fase.</summary>
    internal sealed record Fatos(
        IReadOnlyList<(string Item, DateTimeOffset Instante)> Atos,
        IReadOnlyList<(Guid EtapaId, DateTimeOffset Instante)> Avaliacoes,
        IReadOnlyList<DateTimeOffset> Inscricoes);

    // As versões chegam da mais vigente para a mais antiga.
    internal static HashSet<Guid> Origens(IReadOnlyList<VersaoDasFases> versoes, DateTimeOffset agora, Fatos fatos) =>
    [
        .. versoes.SelectMany(v => v.Fases.Where(f => JanelaAbriu(f, v.SubstituidaEm, agora)).Select(static f => f.OrigemId)),
        .. fatos.Atos.SelectMany(a => FasesDoAto(versoes, a.Item, a.Instante)),
        .. fatos.Avaliacoes.SelectMany(a => FasesDaVersaoEm(versoes, a.Instante).Where(f => f.Etapas.Contains(a.EtapaId))).Select(static f => f.OrigemId),
        .. fatos.Inscricoes.Select(i => FaseQueRecebeu(versoes, i)).OfType<FasesNoEnvelope.Fase>().Select(static f => f.OrigemId),
    ];

    private static bool JanelaAbriu(FasesNoEnvelope.Fase fase, DateTimeOffset? substituidaEm, DateTimeOffset agora) =>
        AbriuAntes(fase.Inicio, substituidaEm, agora) || AbriuAntes(fase.Fim, substituidaEm, agora);

    private static bool AbriuAntes(DateTimeOffset? instante, DateTimeOffset? substituidaEm, DateTimeOffset agora) =>
        instante <= agora && (substituidaEm is null || instante < substituidaEm);

    // O ato guarda o lugar do item que o edital declara: a fase de origem, ou a etapa. O ato da fase vale em qualquer
    // versão, porque a origem é estável; o da etapa vale para a fase a que a etapa pertencia quando o ato foi registrado.
    private static IEnumerable<Guid> FasesDoAto(IReadOnlyList<VersaoDasFases> versoes, string item, DateTimeOffset instante) =>
        versoes.SelectMany(static v => v.Fases).Where(f => DoLugar(item, $"fase:{f.OrigemId:D}")).Concat(
            FasesDaVersaoEm(versoes, instante).Where(f => f.Etapas.Any(e => DoLugar(item, $"etapa:{e:D}")))).Select(static f => f.OrigemId);

    // A inscrição inicia a fase que a recebeu: a que coleta com a janela aberta no instante e, sem janela que a contenha,
    // a primeira que coleta, a mesma que ancora o período de inscrição do edital.
    private static FasesNoEnvelope.Fase? FaseQueRecebeu(IReadOnlyList<VersaoDasFases> versoes, DateTimeOffset instante)
    {
        List<FasesNoEnvelope.Fase> coletam = [.. FasesDaVersaoEm(versoes, instante).Where(static f => f.ColetaInscricao)];
        return coletam.FirstOrDefault(f => f.Inicio <= instante && (f.Fim is null || instante <= f.Fim)) ?? (coletam.Count > 0 ? coletam[0] : null);
    }

    // A versão vigente no instante é a de maior vigência já iniciada nele; antes da primeira vigência vale a mais antiga.
    private static IReadOnlyList<FasesNoEnvelope.Fase> FasesDaVersaoEm(IReadOnlyList<VersaoDasFases> versoes, DateTimeOffset instante) =>
        (versoes.FirstOrDefault(v => v.VigenteAPartirDe <= instante) ?? (versoes.Count > 0 ? versoes[^1] : null))?.Fases ?? [];

    private static bool DoLugar(string item, string lugar) => item.StartsWith(lugar + "|", StringComparison.Ordinal);
}
