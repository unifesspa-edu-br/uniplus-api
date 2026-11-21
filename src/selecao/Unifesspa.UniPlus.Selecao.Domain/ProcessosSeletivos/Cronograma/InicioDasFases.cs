namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

/// <summary>
/// Quais fases e etapas começaram, lendo cada fato do processo na versão publicada que vigia quando ele aconteceu. O
/// período só vale como início se abriu enquanto a versão que o declarava vigia; a importação de notas conta para a fase
/// que a etapa tinha na versão que a importação fixou, e o ato de uma etapa, o envio ao INEP e a inscrição recebida, que não
/// guardam a versão, para a fase da versão vigente no instante do fato. O que começou não deixa de ter começado: a
/// retificação que adia o período ou muda a etapa de fase não apaga o início. A decisão de cada fase é da pergunta
/// <see cref="CondicoesDoInicioDaFase"/>, feita uma vez por fase com o primeiro fato que a iniciaria.
/// </summary>
/// <param name="Versoes">As versões já vigentes, da mais vigente para a mais antiga.</param>
/// <param name="Fatos">Todos os fatos do processo; cada consulta usa só os que já aconteceram no instante dela.</param>
public sealed record InicioDasFases(IReadOnlyList<FasesDaVersaoPublicada> Versoes, FatosDoInicioDasFases Fatos)
{
    /// <summary>As fases de origem que começaram por qualquer fato.</summary>
    public HashSet<Guid> FasesIniciadas(DateTimeOffset agora) =>
        FasesQuePodemComecar(PrimeirosFatos(agora));

    /// <summary>
    /// As fases de origem que começaram por algo que pertence à própria fase: o período dela, o ato dela ou a avaliação
    /// iniciada nela. O período de uma etapa e a inscrição recebida não contam.
    /// </summary>
    public HashSet<Guid> FasesProdutoras(DateTimeOffset agora) =>
        FasesQuePodemComecar(PrimeirosFatosDaPropriaFase(agora));

    /// <summary>
    /// O primeiro fato de cada fase que algum fato iniciaria: o de menor instante; no empate, o da própria fase antes
    /// dos demais, depois a ordem em que os fatos foram listados.
    /// </summary>
    internal IReadOnlyList<FatoQuePodeIniciarAFase> PrimeirosFatos(DateTimeOffset agora) =>
        PrimeiroDeCadaFase([.. FatosDaPropriaFase(agora), .. FatosDeEtapaOuInscricao(agora)]);

    /// <summary>O primeiro fato de cada fase entre os que pertencem à própria fase.</summary>
    internal IReadOnlyList<FatoQuePodeIniciarAFase> PrimeirosFatosDaPropriaFase(DateTimeOffset agora) =>
        PrimeiroDeCadaFase(FatosDaPropriaFase(agora));

    /// <summary>
    /// As etapas que começaram, pela identidade da etapa: o período próprio já abriu enquanto a versão que o declarava
    /// vigia, ou a etapa tem ato, importação de notas, envio ao INEP ou avaliação iniciada próprios. A etapa encerrada
    /// conta, e a abertura de uma etapa não inicia as outras da mesma fase. A etapa sem período só começa por um registro.
    /// Fases e etapas são calculadas de forma independente: a etapa pode ter começado sem que a fase dela tenha começado
    /// pelo mesmo fato.
    /// </summary>
    public HashSet<Guid> EtapasIniciadas(DateTimeOffset agora)
    {
        FatosDoInicioDasFases fatos = Fatos.AteOInstante(agora);

        return
        [
            .. EtapasComPeriodoAberto(agora),
            .. fatos.Atos.Select(static a => a.EtapaProcessoId).OfType<Guid>().Where(e => Versoes.Any(v => v.TemAEtapa(e))),
            .. fatos.Importacoes.Select(static i => i.EtapaId),
            .. fatos.EnviosAoInep.Select(static e => e.EtapaId),
            .. fatos.AvaliacoesIniciadas.Select(static a => a.Lugar.EtapaProcessoId).OfType<Guid>(),
        ];
    }

    private static HashSet<Guid> FasesQuePodemComecar(IEnumerable<FatoQuePodeIniciarAFase> primeirosFatos) =>
    [
        .. primeirosFatos
            .Where(static f => CondicoesDoInicioDaFase.PodeComecar(f).IsSuccess)
            .Select(static f => f.FaseCanonicaOrigemId),
    ];

    private static List<FatoQuePodeIniciarAFase> PrimeiroDeCadaFase(IEnumerable<FatoQuePodeIniciarAFase> fatos) =>
    [
        .. fatos
            .GroupBy(static f => f.FaseCanonicaOrigemId)
            .Select(static g => g.MinBy(static f => f.Instante)!),
    ];

    /// <summary>Os fatos que pertencem à própria fase: o período dela, o ato dela e a avaliação iniciada nela.</summary>
    internal List<FatoQuePodeIniciarAFase> FatosDaPropriaFase(DateTimeOffset agora)
    {
        FatosDoInicioDasFases fatos = Fatos.AteOInstante(agora);

        return
        [
            .. PeriodosDasFases(agora),
            .. fatos.Atos.SelectMany(DoAtoNoLugarDaFase),
            .. fatos.AvaliacoesIniciadas.SelectMany(static a => DaAvaliacaoNaFase(a)),
        ];
    }

    /// <summary>Os fatos que iniciam a fase sem serem dela: o de uma etapa que a contém e a inscrição recebida.</summary>
    internal List<FatoQuePodeIniciarAFase> FatosDeEtapaOuInscricao(DateTimeOffset agora)
    {
        FatosDoInicioDasFases fatos = Fatos.AteOInstante(agora);

        return
        [
            .. PeriodosDasEtapas(agora),
            .. fatos.Atos.SelectMany(DoAtoNoLugarDaEtapa),
            .. fatos.Importacoes.SelectMany(DaImportacaoDeNotas),
            .. fatos.EnviosAoInep.SelectMany(e => DaEtapaNaVersaoVigente(e.EtapaId, e.Instante)),
            .. fatos.InscricoesRecebidas.SelectMany(DaInscricaoRecebida),
            .. fatos.AvaliacoesIniciadas.SelectMany(DaAvaliacaoNaEtapa),
        ];
    }

    private IEnumerable<FatoQuePodeIniciarAFase> PeriodosDasFases(DateTimeOffset agora) =>
        Versoes.Where(static v => v.Vigorou).SelectMany(v => v.Fases.SelectMany(f => DoPeriodoAberto(v, f, [f.Periodo.Inicio, f.Periodo.Fim], agora)));

    private IEnumerable<FatoQuePodeIniciarAFase> PeriodosDasEtapas(DateTimeOffset agora) =>
        Versoes.Where(static v => v.Vigorou).SelectMany(v => v.Fases.SelectMany(f => DoPeriodoAberto(v, f, DatasDasEtapas(f), agora)));

    private HashSet<Guid> EtapasComPeriodoAberto(DateTimeOffset agora) =>
    [
        .. Versoes
            .Where(static v => v.Vigorou)
            .SelectMany(v => v.Fases
                .SelectMany(static f => f.Etapas)
                .Where(e => v.PrimeiraDataQueChegouEnquantoVigia([e.Periodo.Inicio, e.Periodo.Fim], agora) is not null)
                .Select(static e => e.Id)),
    ];

    private static IEnumerable<DateTimeOffset?> DatasDasEtapas(FaseDaVersaoPublicada fase) =>
        fase.Etapas.SelectMany(static e => new[] { e.Periodo.Inicio, e.Periodo.Fim });

    // O instante que conta é o maior entre a data declarada e o início da vigência: a versão que passa a vigir no dia 10 e
    // declara período iniciado no dia 5 só tem o período em andamento a partir do dia 10.
    private static IEnumerable<FatoQuePodeIniciarAFase> DoPeriodoAberto(
        FasesDaVersaoPublicada versao, FaseDaVersaoPublicada fase, IEnumerable<DateTimeOffset?> datas, DateTimeOffset agora) =>
        versao.PrimeiraDataQueChegouEnquantoVigia(datas, agora) is { } data
            ? [new FatoQuePodeIniciarAFase(fase.FaseCanonicaOrigemId, versao, data > versao.VigenteAPartirDe ? data : versao.VigenteAPartirDe)]
            : [];

    // O ato da fase vale para a fase em qualquer versão em que ela aparece, porque a origem é estável.
    private IEnumerable<FatoQuePodeIniciarAFase> DoAtoNoLugarDaFase(AtoPublicadoNoLugar ato) =>
        ato.FaseCanonicaOrigemId is { } origem && Versoes.Any(v => v.TemAFase(origem))
            ? [new FatoQuePodeIniciarAFase(origem, null, ato.Instante)]
            : [];

    // A avaliação iniciada na fase vale para a fase direto, sem conferir versão nem a existência dela no cronograma.
    private static IEnumerable<FatoQuePodeIniciarAFase> DaAvaliacaoNaFase(AvaliacaoIniciadaNoLugar avaliacao) =>
        avaliacao.Lugar.FaseCanonicaOrigemId is { } origem
            ? [new FatoQuePodeIniciarAFase(origem, null, avaliacao.Instante)]
            : [];

    // O ato da etapa vale para a fase a que a etapa pertencia quando o ato foi publicado.
    private IEnumerable<FatoQuePodeIniciarAFase> DoAtoNoLugarDaEtapa(AtoPublicadoNoLugar ato) =>
        ato.EtapaProcessoId is { } etapa ? DaEtapaNaVersaoVigente(etapa, ato.Instante) : [];

    private IEnumerable<FatoQuePodeIniciarAFase> DaAvaliacaoNaEtapa(AvaliacaoIniciadaNoLugar avaliacao) =>
        avaliacao.Lugar.EtapaProcessoId is { } etapa ? DaEtapaNaVersaoVigente(etapa, avaliacao.Instante) : [];

    // A importação executa contra a versão divulgada que a importação fixou, que pode ser anterior a uma retificação já fechada e
    // ainda não divulgada; só se essa versão não estiver entre as vigentes vale a versão vigente no instante da importação.
    private IEnumerable<FatoQuePodeIniciarAFase> DaImportacaoDeNotas(ImportacaoDeNotasNaEtapa importacao) =>
        DaEtapaNaVersao(
            Versoes.FirstOrDefault(v => v.VersaoId == importacao.VersaoId) ?? VersaoVigenteEm(importacao.Instante),
            importacao.EtapaId,
            importacao.Instante);

    // A inscrição inicia a fase que a recebeu na versão vigente no instante em que foi recebida.
    private IEnumerable<FatoQuePodeIniciarAFase> DaInscricaoRecebida(DateTimeOffset instante)
    {
        FasesDaVersaoPublicada? versao = VersaoVigenteEm(instante);

        return versao?.FaseQueRecebeInscricaoEm(instante) is { } fase
            ? [new FatoQuePodeIniciarAFase(fase.FaseCanonicaOrigemId, versao, instante)]
            : [];
    }

    private IEnumerable<FatoQuePodeIniciarAFase> DaEtapaNaVersaoVigente(Guid etapaId, DateTimeOffset instante) =>
        DaEtapaNaVersao(VersaoVigenteEm(instante), etapaId, instante);

    // Cada fase que contém a etapa na versão gera o seu fato; a etapa fora da versão não inicia fase nenhuma.
    private static IEnumerable<FatoQuePodeIniciarAFase> DaEtapaNaVersao(FasesDaVersaoPublicada? versao, Guid etapaId, DateTimeOffset instante) =>
        versao is null
            ? []
            : versao.FasesComAEtapa(etapaId).Select(f => new FatoQuePodeIniciarAFase(f.FaseCanonicaOrigemId, versao, instante));

    // A versão vigente no instante é a de maior vigência já iniciada nele; antes da primeira vigência vale a mais antiga.
    private FasesDaVersaoPublicada? VersaoVigenteEm(DateTimeOffset instante) =>
        Versoes.FirstOrDefault(v => v.VigenteAPartirDe <= instante) ?? (Versoes.Count > 0 ? Versoes[^1] : null);
}
