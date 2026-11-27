namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Retificacao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Retificacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>Monta as fases da versão publicada e os fatos da execução que as conferências da retificação comparam.</summary>
internal static class FaseDaRetificacaoDeTeste
{
    internal static readonly DateTimeOffset Instante = new(2027, 3, 10, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid TipoDaEtapaDeTeste = Guid.Parse("0197d6f0-0000-7000-8000-00000000e7a0");

    internal static ProdutoDaFase Preliminar(string ato) => ProdutoDaFase.Criar(ato, PapelDaPublicacaoPrevista.Preliminar);

    internal static ProdutoDaFase Definitivo(string ato) => ProdutoDaFase.Criar(ato, PapelDaPublicacaoPrevista.Definitivo);

    internal static FaseCronograma Fase(
        int ordem,
        string codigo,
        (DateTimeOffset Inicio, DateTimeOffset Fim)? janela = null,
        bool coletaInscricao = false,
        bool coletaIsencao = false,
        bool noMesmoMomentoDaAnterior = false,
        params ProdutoDaFase[] produtos) =>
        FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
            OrigensCanonicasDeTeste.Da(Guid.CreateVersion7(), codigo, OrigemDataFase.NoEdital, agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: coletaInscricao, coletaSolicitacaoIsencao: coletaIsencao),
            new DecisoesDeCronogramaDaFase(ordem, Inicio: janela?.Inicio ?? Instante.AddDays(ordem), Fim: janela?.Fim ?? Instante.AddDays(ordem + 5), UnidadeResponsavel: UnidadesDeTeste.Ceps)
            { NoMesmoMomentoDaAnterior = noMesmoMomentoDaAnterior },
            new PublicacoesDaFase(Produtos: produtos, EmiteParecerIndividual: false, RegraRecurso: null),
            bancasRequeridas: []).Value!;

    /// <summary>A fase de data definida na execução, que começa no instante dado e ainda não tem data final.</summary>
    internal static FaseCronograma SemDataFinal(int ordem, string codigo, DateTimeOffset inicio) =>
        FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
            OrigensCanonicasDeTeste.Da(Guid.CreateVersion7(), codigo, OrigemDataFase.NaExecucao, agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: false, coletaSolicitacaoIsencao: false),
            new DecisoesDeCronogramaDaFase(ordem, Inicio: inicio, Fim: null, UnidadeResponsavel: UnidadesDeTeste.Ceps),
            new PublicacoesDaFase(Produtos: [], EmiteParecerIndividual: false, RegraRecurso: null),
            bancasRequeridas: []).Value!;

    /// <summary>A mesma fase, com a mesma origem canônica, com a janela trocada: a fase como a retificação a propõe.</summary>
    internal static FaseCronograma ComJanela(FaseCronograma fase, DateTimeOffset? inicio, DateTimeOffset? fim) =>
        FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
            OrigensCanonicasDeTeste.Da(fase.FaseCanonicaOrigemId, fase.Codigo, fase.OrigemData, fase.AgrupaEtapas, fase.PermiteComplementacao, fase.ColetaInscricao, fase.ColetaSolicitacaoIsencao),
            new DecisoesDeCronogramaDaFase(fase.Ordem, Inicio: inicio, Fim: fim, UnidadeResponsavel: UnidadesDeTeste.Ceps)
            { NoMesmoMomentoDaAnterior = fase.NoMesmoMomentoDaAnterior },
            new PublicacoesDaFase(Produtos: [.. fase.Produtos], EmiteParecerIndividual: false, RegraRecurso: null),
            bancasRequeridas: []).Value!;

    /// <summary>A mesma fase, com a mesma origem canônica e os mesmos dados, com a marca "no mesmo momento da anterior" trocada.</summary>
    internal static FaseCronograma ComMarca(FaseCronograma fase, bool noMesmoMomentoDaAnterior) =>
        FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
            OrigensCanonicasDeTeste.Da(fase.FaseCanonicaOrigemId, fase.Codigo, fase.OrigemData, fase.AgrupaEtapas, fase.PermiteComplementacao, fase.ColetaInscricao, fase.ColetaSolicitacaoIsencao),
            new DecisoesDeCronogramaDaFase(fase.Ordem, Inicio: fase.Inicio, Fim: fase.Fim, UnidadeResponsavel: UnidadesDeTeste.Ceps)
            { NoMesmoMomentoDaAnterior = noMesmoMomentoDaAnterior },
            new PublicacoesDaFase(Produtos: [.. fase.Produtos], EmiteParecerIndividual: false, RegraRecurso: null),
            bancasRequeridas: []).Value!;

    /// <summary>A mesma fase, com a mesma origem canônica e a mesma janela, declarando os tipos de decisão dados.</summary>
    internal static FaseCronograma ComTiposDeDecisao(FaseCronograma fase, params string[] tipos) =>
        FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
            OrigensCanonicasDeTeste.Da(fase.FaseCanonicaOrigemId, fase.Codigo, fase.OrigemData, fase.AgrupaEtapas, fase.PermiteComplementacao, fase.ColetaInscricao, fase.ColetaSolicitacaoIsencao),
            new DecisoesDeCronogramaDaFase(fase.Ordem, Inicio: fase.Inicio, Fim: fase.Fim, UnidadeResponsavel: UnidadesDeTeste.Ceps)
            { NoMesmoMomentoDaAnterior = fase.NoMesmoMomentoDaAnterior },
            new PublicacoesDaFase(Produtos: [.. fase.Produtos], EmiteParecerIndividual: false, RegraRecurso: null, TiposDeDecisao: tipos),
            bancasRequeridas: []).Value!;

    /// <summary>A mesma fase, com a mesma origem canônica, com outro código, outra ordem e outros produtos: o que não entra no preliminar.</summary>
    internal static FaseCronograma Renomeada(FaseCronograma fase, string codigo, int ordem, params ProdutoDaFase[] produtos) =>
        FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
            OrigensCanonicasDeTeste.Da(fase.FaseCanonicaOrigemId, codigo, fase.OrigemData, fase.AgrupaEtapas, fase.PermiteComplementacao, fase.ColetaInscricao, fase.ColetaSolicitacaoIsencao),
            new DecisoesDeCronogramaDaFase(ordem, Inicio: fase.Inicio, Fim: fase.Fim, UnidadeResponsavel: UnidadesDeTeste.Ceps)
            { NoMesmoMomentoDaAnterior = fase.NoMesmoMomentoDaAnterior },
            new PublicacoesDaFase(Produtos: produtos, EmiteParecerIndividual: false, RegraRecurso: null),
            bancasRequeridas: []).Value!;

    internal static FasesIniciadas Iniciadas(params FaseCronograma[] fases) =>
        new(fases.Select(static f => KeyValuePair.Create(f.FaseCanonicaOrigemId, f.UnidadeResponsavel?.OrigemId)));

    internal static FatosDaExecucao Fatos(FasesIniciadas iniciadas, params IdentidadeDoItemDoEdital[] publicados) =>
        new() { Instante = Instante, FasesIniciadas = iniciadas, ItensPublicados = new HashSet<IdentidadeDoItemDoEdital>(publicados) };

    internal static IdentidadeDoItemDoEdital Item(FaseCronograma fase, ProdutoDaFase produto) => IdentidadeDoItemDoEdital.DaFase(fase, produto);

    internal static EtapaProcesso EtapaComJanela(string nome, DateTimeOffset inicio, int ordem)
    {
        EtapaProcesso etapa = EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
            new IdentificacaoDaEtapa(Nome: nome, Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: ordem, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: 10m, PassoDoLancamento: null, FormaDeAvaliacao: FormaDeAvaliacao.Objetiva, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;
        etapa.DefinirJanelaEParecer(inicio, inicio.AddDays(3), emiteParecerIndividual: false).IsSuccess.Should().BeTrue();

        return etapa;
    }

    /// <summary>A etapa de identidade fixa, com peso e produtos: a mesma etapa em versões diferentes tem o mesmo <c>Id</c>.</summary>
    internal static EtapaProcesso EtapaComPeso(Guid id, string nome, decimal peso, string faseCodigo, params ProdutoDaEtapa[] produtos)
    {
        EtapaProcesso etapa = EtapaProcesso.Reidratar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
            id,
            new IdentificacaoDaEtapa(Nome: nome, Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(TipoDaEtapaDeTeste, "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: 1, FaseCodigo: faseCodigo),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: peso, NotaMinima: null, NotaMaxima: 10m, PassoDoLancamento: null, FormaDeAvaliacao: FormaDeAvaliacao.Objetiva, RegraConsolidacao: null, EscalaDeAtendimento: null), itens: []);
        etapa.DefinirProdutos(produtos).IsSuccess.Should().BeTrue();

        return etapa;
    }
}
