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

    internal static ProdutoDaFase Preliminar(string ato) => ProdutoDaFase.Criar(ato, PapelProdutoFase.Preliminar);

    internal static ProdutoDaFase Definitivo(string ato) => ProdutoDaFase.Criar(ato, PapelProdutoFase.Definitivo);

    internal static FaseCronograma Fase(
        int ordem,
        string codigo,
        (DateTimeOffset Inicio, DateTimeOffset Fim)? janela = null,
        bool coletaInscricao = false,
        bool coletaIsencao = false,
        string? faseConcluinteCodigo = null,
        params ProdutoDaFase[] produtos) =>
        FaseCronograma.Criar(
            new OrigemCanonicaDaFase(Guid.CreateVersion7(), codigo, OrigemDataFase.NoEdital, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: coletaInscricao, ColetaSolicitacaoIsencao: coletaIsencao),
            new DecisoesDeCronogramaDaFase(ordem, Inicio: janela?.Inicio ?? Instante.AddDays(ordem), Fim: janela?.Fim ?? Instante.AddDays(ordem + 5), FaseConcluinteCodigo: faseConcluinteCodigo, UnidadeResponsavel: UnidadesDeTeste.Ceps),
            new PublicacoesDaFase(Produtos: produtos, EmiteParecerIndividual: false, RegraRecurso: null),
            bancasRequeridas: []).Value!;

    /// <summary>A mesma fase, com a mesma origem canônica, com a janela trocada: a fase como a retificação a propõe.</summary>
    internal static FaseCronograma ComJanela(FaseCronograma fase, DateTimeOffset? inicio, DateTimeOffset? fim) =>
        FaseCronograma.Criar(
            new OrigemCanonicaDaFase(fase.FaseCanonicaOrigemId, fase.Codigo, fase.OrigemData, fase.AgrupaEtapas, fase.PermiteComplementacao, fase.ColetaInscricao, fase.ColetaSolicitacaoIsencao),
            new DecisoesDeCronogramaDaFase(fase.Ordem, Inicio: inicio, Fim: fim, FaseConcluinteCodigo: fase.FaseConcluinteCodigo, UnidadeResponsavel: UnidadesDeTeste.Ceps),
            new PublicacoesDaFase(Produtos: [.. fase.Produtos], EmiteParecerIndividual: false, RegraRecurso: null),
            bancasRequeridas: []).Value!;

    internal static FasesIniciadas Iniciadas(params FaseCronograma[] fases) =>
        new(fases.Select(static f => KeyValuePair.Create(f.FaseCanonicaOrigemId, f.UnidadeResponsavelOrigemId)));

    internal static FatosDaExecucao Fatos(FasesIniciadas iniciadas, params IdentidadeDoItemDoEdital[] publicados) =>
        new() { Instante = Instante, FasesIniciadas = iniciadas, ItensPublicados = new HashSet<IdentidadeDoItemDoEdital>(publicados) };

    internal static IdentidadeDoItemDoEdital Item(FaseCronograma fase, ProdutoDaFase produto) => IdentidadeDoItemDoEdital.DaFase(fase, produto);

    internal static EtapaProcesso EtapaComJanela(string nome, DateTimeOffset inicio, string? faseCodigo = null)
    {
        EtapaProcesso etapa = EtapaProcesso.Criar(
            new IdentificacaoDaEtapa(Nome: nome, Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: null, FaseCodigo: faseCodigo),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: 10m, PassoDoLancamento: null, FormaDeAvaliacao: FormaDeAvaliacao.Objetiva, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;
        etapa.DefinirJanelaEParecer(inicio, inicio.AddDays(3), emiteParecerIndividual: false).IsSuccess.Should().BeTrue();

        return etapa;
    }
}
