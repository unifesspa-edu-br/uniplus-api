namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Bancas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

/// <summary>
/// A exigência de banca de recursos e o mínimo de integrantes, sobre o estado do processo: a mesma
/// pendência alimenta a conferência de congelamento e o painel.
/// </summary>
public sealed class BancasDoProcessoTests
{
    private static readonly Guid Tipo = new("0197d6f0-0000-7000-8000-00000000b4c1");

    private static RegraRecursoFase RegraDeRecurso() => RegraRecursoFase.Criar(
        ReferenciaRegra.De(RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", new string('c', 64)),
        new ArgsRegraPrazoRecurso(2m, UnidadePrazo.DiasUteis, null, null, false, null, null),
        ProcessoConformeFactory.ProdutoPreliminarId).Value!;

    private static readonly ReferenciaRegra CorrecaoIndependente =
        ReferenciaRegra.De(RegraConsolidacaoCodigo.CorrecaoIndependente, "v1", new string('d', 64));

    private static readonly ReferenciaRegra MaioriaSimples =
        ReferenciaRegra.De(RegraConsolidacaoCodigo.ConceitoMaioriaSimples, "v1", new string('f', 64));

    private static readonly ReferenciaRegra AvaliacaoUnica =
        ReferenciaRegra.De(RegraConsolidacaoCodigo.AvaliacaoUnica, "v1", new string('e', 64));

    private static EtapaProcesso NovaEtapa(string nome, int ordem, ReferenciaRegra? consolidacao = null) => EtapaProcesso.Criar(
        new IdentificacaoDaEtapa(Nome: nome, Carater: CaraterEtapa.Classificatoria, TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!, Ordem: ordem, FaseCodigo: "RESULTADO_FINAL"),
        new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: 10m, PassoDoLancamento: null, FormaDeAvaliacao: consolidacao is null ? FormaDeAvaliacao.Objetiva : FormaDeAvaliacao.Humana, RegraConsolidacao: consolidacao, EscalaDeAtendimento: null)).Value!;

    /// <summary>Dá à etapa o par preliminar e definitivo e uma janela de recurso ancorada no preliminar.</summary>
    private static EtapaProcesso ComRecurso(EtapaProcesso etapa)
    {
        etapa.DefinirProdutos(
        [
            ProdutoDaEtapa.Criar("RESULTADO_PROVA", PapelProdutoFase.Preliminar),
            ProdutoDaEtapa.Criar("RESULTADO_PROVA", PapelProdutoFase.Definitivo),
        ]).IsSuccess.Should().BeTrue();
        Guid preliminar = etapa.Produtos.Single(p => p.Papel == PapelProdutoFase.Preliminar).Id;
        etapa.DefinirRecursos([RecursoDaEtapa.Criar(
            AncoraDoRecurso.AtoPublicado, RegraDeRecurso().Regra, RegraDeRecurso().Args, preliminar).Value!]).IsSuccess.Should().BeTrue();
        return etapa;
    }

    private static ProcessoSeletivo ProcessoComEtapas(IReadOnlyList<BancaRequerida>? bancasDaFase, params EtapaProcesso[] etapas)
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar(fase: ProcessoConformeFactory.FaseConforme(bancas: bancasDaFase ?? []));
        processo.DefinirEtapas(etapas, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        return processo;
    }

    private static BancaDaEtapa DeRecursosDaEtapa(int integrantes, int quorum) =>
        BancasDeTeste.DeRecursosDaEtapa(BancasDeTeste.Regra(integrantes, quorum));

    private static BancaRequerida DeRecursosDaFase(int integrantes, int quorum) =>
        BancaRequerida.Criar(Tipo, TipoBancaCodigo.AnaliseDeRecursos, "Banca de análise de recursos", [], BancasDeTeste.Regra(integrantes, quorum));

    private static CorretoresDasEtapas Corretores(params (ReferenciaRegra Regra, int Numero)[] porRegra) =>
        new(porRegra.ToDictionary(static par => par.Regra, static par => CorretoresDaRegra.SemNovoCorretor(par.Numero)));

    private static DomainError? Pendencia(ProcessoSeletivo processo, CorretoresDasEtapas? corretores = null) =>
        ConferenciaDasBancas.ConferirRecursosSemBanca(CronogramaDoProcesso.Do(processo.CronogramaFases), EtapasDoProcesso.Das(processo.Etapas))
            .Concat(CronogramaDoProcesso.Do(processo.CronogramaFases).RecusasDasDeclaracoes(ExigenciaDeBancaDeRecurso.CodigosDoRecorte))
            .Concat(ConferenciaDasBancas.ConferirRegrasDeDeliberacao(CronogramaDoProcesso.Do(processo.CronogramaFases), EtapasDoProcesso.Das(processo.Etapas)))
            .Concat(Minimo(processo, corretores ?? CorretoresDeTeste.SemConsolidacao))
            .Select(static causa => causa.Error)
            .FirstOrDefault();

    private static IEnumerable<string> CodigosDaConferencia(ProcessoSeletivo processo, CorretoresDasEtapas corretores) =>
        ConfiguracaoAptaACongelar.Conferir(EstadoDaConfiguracao.De(processo), CadastrosDeTeste.Com(corretores: corretores)).Errors
            .Where(static causa => causa.Error.Code != "ProcessoSeletivo.ConformidadeInsuficiente"
                && causa.Field?.StartsWith("identificacao", StringComparison.Ordinal) != true)
            .Select(static causa => causa.Error.Code);

    private static IReadOnlyList<ItemConformidade> Itens(ProcessoSeletivo processo, CorretoresDasEtapas corretores) =>
        ConferenciaDasBancas.Itens(CronogramaDoProcesso.Do(processo.CronogramaFases), EtapasDoProcesso.Das(processo.Etapas), corretores);

    private static IReadOnlyList<FieldError> Minimo(ProcessoSeletivo processo, CorretoresDasEtapas corretores) =>
        ConferenciaDasBancas.ConferirMinimoDeIntegrantes(CronogramaDoProcesso.Do(processo.CronogramaFases), EtapasDoProcesso.Das(processo.Etapas), corretores);

    [Fact(DisplayName = "Fase com recurso sem banca de análise de recursos é recusada, citando a fase")]
    public void FaseComRecursoSemBanca_RecusaCitandoAFase()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar(
            fase: ProcessoConformeFactory.FaseConforme(RegraDeRecurso(), bancas: []));

        DomainError? pendencia = Pendencia(processo);

        pendencia!.Code.Should().Be("Cronograma.RecursoSemBanca");
        pendencia.Message.Should().Contain("RESULTADO_FINAL");
    }

    [Fact(DisplayName = "Fase com recurso e banca de análise de recursos declarada é aceita")]
    public void FaseComRecursoEBanca_Aceita()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar(fase: ProcessoConformeFactory.FaseConforme(RegraDeRecurso()));

        Pendencia(processo).Should().BeNull();
    }

    [Fact(DisplayName = "Etapa com recurso e banca de recursos só na fase dela é aceita")]
    public void EtapaComRecursoEBancaNaFase_Aceita()
    {
        ProcessoSeletivo processo = ProcessoComEtapas([BancasDeTeste.DeRecursosDaFase()], ComRecurso(NovaEtapa("Redação", 1)));

        Pendencia(processo).Should().BeNull();
    }

    [Fact(DisplayName = "Etapa com recurso e banca de recursos só em outra etapa é recusada, citando a etapa")]
    public void EtapaComRecursoEBancaSoEmOutraEtapa_RecusaCitandoAEtapa()
    {
        EtapaProcesso semBanca = ComRecurso(NovaEtapa("Redação", 1));
        EtapaProcesso comBanca = ComRecurso(NovaEtapa("Objetiva", 2));
        ProcessoSeletivo processo = ProcessoComEtapas(null, semBanca, comBanca);
        processo.Etapas.Single(e => e.Nome == "Objetiva").DefinirBancas([BancasDeTeste.DeRecursosDaEtapa()]).IsSuccess.Should().BeTrue();

        DomainError? pendencia = Pendencia(processo);

        pendencia!.Code.Should().Be("Cronograma.RecursoSemBanca");
        pendencia.Message.Should().Contain("Redação");
    }

    // A fase criada recusa o recorte na banca de recursos; o estado violado chega pela reidratação, como o que o banco hidrata.
    private static FaseCronograma FaseReidratadaComBancaDeRecursosComRecorte(BancaRequerida banca) => FaseCronograma.Reidratar(
        Guid.CreateVersion7(),
        new OrigemCanonicaDaFase(Guid.CreateVersion7(), "RESULTADO_FINAL", OrigemDataFase.NoEdital, true, false, true, false),
        new DecisoesDeCronogramaDaFase(
            1, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), null, UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(
            [
                ProdutoDaFase.Reidratar(ProcessoConformeFactory.ProdutoPreliminarId, "RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar),
                ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo),
            ],
            false,
            RegraDeRecurso()),
        [banca]);

    [Fact(DisplayName = "Banca de análise de recursos com recorte de competência é recusada")]
    public void BancaDeRecursosComRecorte_Recusa()
    {
        BancaRequerida comRecorte = BancaRequerida.Criar(
            Tipo, TipoBancaCodigo.AnaliseDeRecursos, "Banca de análise de recursos",
            [CategoriaJulgada.Criar(Guid.CreateVersion7(), "RENDA")], BancasDeTeste.Regra());
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar(fase: FaseReidratadaComBancaDeRecursosComRecorte(comRecorte));

        Pendencia(processo)!.Code.Should().Be("Banca.RecorteEmBancaDeRecursos");
        ExigenciaDeBancaDeRecurso.RecorteNaDeclaracao("RECURSOS", TipoBancaCodigo.AnaliseDeRecursos, 1)!.Code
            .Should().Be("Banca.RecorteEmBancaDeRecursos");
        ExigenciaDeBancaDeRecurso.RecorteNaDeclaracao("RECURSOS", "BANCA_ANALISE_DOCUMENTAL", 1).Should().BeNull();
    }

    [Fact(DisplayName = "Banca da etapa sem regra de deliberação é recusada, citando o nome da banca")]
    public void BancaDaEtapaSemRegra_Recusa()
    {
        EtapaProcesso etapa = NovaEtapa("Redação", 1);
        ProcessoSeletivo processo = ProcessoComEtapas(null, etapa);
        etapa.DefinirBancas([BancaDaEtapa.Criar(Tipo, "BANCA_EXAMINADORA", "Banca examinadora")]).IsSuccess.Should().BeTrue();

        DomainError? pendencia = Pendencia(processo);

        pendencia!.Code.Should().Be("Banca.RegraDeDeliberacaoObrigatoria");
        pendencia.Message.Should().Contain("Banca examinadora");
    }

    [Fact(DisplayName = "Banca da fase sem regra de deliberação é recusada")]
    public void BancaDaFaseSemRegra_Recusa()
    {
        BancaRequerida semRegra = BancaRequerida.Criar(Tipo, "BANCA_ANALISE_DOCUMENTAL", "Análise documental", []);
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar(fase: ProcessoConformeFactory.FaseConforme(bancas: [semRegra]));

        Pendencia(processo)!.Code.Should().Be("Banca.RegraDeDeliberacaoObrigatoria");
    }

    [Fact(DisplayName = "Redação com três corretores e banca de 2 titulares com quórum 2 é recusada, citando mínimo e valor declarado")]
    public void BancaCurta_ParaTresCorretores_Recusa()
    {
        EtapaProcesso redacao = ComRecurso(NovaEtapa("Redação", 1, CorrecaoIndependente));
        ProcessoSeletivo processo = ProcessoComEtapas(null, redacao);
        redacao.DefinirBancas([DeRecursosDaEtapa(integrantes: 2, quorum: 2)]).IsSuccess.Should().BeTrue();

        DomainError? pendencia = Pendencia(processo, Corretores((CorrecaoIndependente, 3)));

        pendencia!.Code.Should().Be("Banca.IntegrantesAbaixoDoMinimo");
        pendencia.Message.Should().Contain("declara 2 integrantes").And.Contain("o mínimo exigido é 3");
    }

    [Theory(DisplayName = "Banca da etapa aceita quando integrantes alcançam o maior entre o quórum e os corretores")]
    [InlineData(3, 2, 3)]
    [InlineData(5, 4, 3)]
    [InlineData(3, 2, 0)]
    [InlineData(1, 1, 1)]
    public void BancaDaEtapa_NoMinimo_Aceita(int integrantes, int quorum, int corretores)
    {
        EtapaProcesso etapa = ComRecurso(NovaEtapa("Redação", 1, corretores == 0 ? null : CorrecaoIndependente));
        ProcessoSeletivo processo = ProcessoComEtapas(null, etapa);
        etapa.DefinirBancas([DeRecursosDaEtapa(integrantes, quorum)]).IsSuccess.Should().BeTrue();

        Pendencia(processo, Corretores((CorrecaoIndependente, corretores))).Should().BeNull();
    }

    [Theory(DisplayName = "Banca da etapa de maioria simples com novo corretor exige os três corretores e mais um, e aceita quando os alcança")]
    [InlineData(3, false)]
    [InlineData(4, true)]
    public void BancaDaEtapa_ComNovoCorretor_ExigeOsCorretoresMaisUm(int integrantes, bool aceita)
    {
        EtapaProcesso etapa = ComRecurso(NovaEtapa("Entrevista", 1, MaioriaSimples));
        ProcessoSeletivo processo = ProcessoComEtapas(null, etapa);
        etapa.DefinirBancas([DeRecursosDaEtapa(integrantes, quorum: 2)]).IsSuccess.Should().BeTrue();

        DomainError? pendencia = Pendencia(processo, Corretores((MaioriaSimples, 4)));

        (pendencia is null).Should().Be(aceita);
        pendencia?.Message.Should().Contain($"declara {integrantes} integrantes").And.Contain("o mínimo exigido é 4");
    }

    [Fact(DisplayName = "Quórum maior que os corretores eleva o mínimo ao quórum")]
    public void Minimo_ComQuorumMaiorQueOsCorretores_ExigeOQuorum()
    {
        EtapaProcesso redacao = NovaEtapa("Redação", 1, CorrecaoIndependente);

        MinimoDeIntegrantes.Exigido(BancasDeTeste.Regra(integrantes: 5, quorum: 4), [Corretores((CorrecaoIndependente, 3)).EmCampo(redacao)])
            .Should().Be(4);
    }

    [Fact(DisplayName = "Banca da fase que julga uma etapa de avaliação única e outra de correção independente usa o maior número de corretores")]
    public void BancaDaFase_JulgandoDuasEtapas_UsaOMaiorNumeroDeCorretores()
    {
        EtapaProcesso redacao = ComRecurso(NovaEtapa("Redação", 1, CorrecaoIndependente));
        EtapaProcesso entrevista = ComRecurso(NovaEtapa("Entrevista", 2, AvaliacaoUnica));
        ProcessoSeletivo processo = ProcessoComEtapas([DeRecursosDaFase(integrantes: 2, quorum: 2)], redacao, entrevista);

        DomainError? pendencia = Pendencia(processo, Corretores((CorrecaoIndependente, 3), (AvaliacaoUnica, 1)));

        pendencia!.Code.Should().Be("Banca.IntegrantesAbaixoDoMinimo");
        pendencia.Message.Should().Contain("o mínimo exigido é 3");
    }

    [Fact(DisplayName = "Banca da etapa julga só a própria etapa, mesmo que outra exija mais corretores")]
    public void BancaDaEtapa_JulgaSoAPropriaEtapa()
    {
        EtapaProcesso redacao = ComRecurso(NovaEtapa("Redação", 1, CorrecaoIndependente));
        EtapaProcesso entrevista = ComRecurso(NovaEtapa("Entrevista", 2, AvaliacaoUnica));
        ProcessoSeletivo processo = ProcessoComEtapas([DeRecursosDaFase(integrantes: 3, quorum: 2)], redacao, entrevista);
        processo.Etapas.Single(e => e.Nome == "Entrevista").DefinirBancas([DeRecursosDaEtapa(integrantes: 1, quorum: 1)]).IsSuccess.Should().BeTrue();

        Pendencia(processo, Corretores((CorrecaoIndependente, 3), (AvaliacaoUnica, 1))).Should().BeNull();
    }

    [Fact(DisplayName = "Banca da etapa que não é de recursos não julga a etapa e não herda o mínimo de corretores")]
    public void BancaDaEtapaQueNaoEDeRecursos_NaoHerdaOMinimoDeCorretores()
    {
        EtapaProcesso redacao = ComRecurso(NovaEtapa("Redação", 1, CorrecaoIndependente));
        ProcessoSeletivo processo = ProcessoComEtapas([BancasDeTeste.DeRecursosDaFase()], redacao);
        redacao.DefinirBancas([BancaDaEtapa.Criar(Tipo, "BANCA_EXAMINADORA", "Banca examinadora", BancasDeTeste.Regra(integrantes: 2, quorum: 2))])
            .IsSuccess.Should().BeTrue();

        Pendencia(processo, Corretores((CorrecaoIndependente, 3))).Should().BeNull();
    }

    [Fact(DisplayName = "O painel projeta a mesma pendência da conferência em cada item das bancas")]
    public void Painel_ProjetaAPendenciaDaConferencia()
    {
        EtapaProcesso redacao = ComRecurso(NovaEtapa("Redação", 1, CorrecaoIndependente));
        ProcessoSeletivo processo = ProcessoComEtapas(null, redacao);
        redacao.DefinirBancas([DeRecursosDaEtapa(integrantes: 2, quorum: 2)]).IsSuccess.Should().BeTrue();
        CorretoresDasEtapas corretores = Corretores((CorrecaoIndependente, 3));

        IEnumerable<string> vermelhos = Itens(processo, corretores)
            .Where(static item => !item.Ok).Select(static item => item.Codigo);

        vermelhos.Should().Equal("cronograma_banca_com_o_minimo_de_integrantes");
        Itens(processo, Corretores((CorrecaoIndependente, 2))).Should().OnlyContain(static item => item.Ok);
    }

    [Fact(DisplayName = "Etapa com regra de consolidação não resolvida gera recusa própria, citando a etapa, e nunca vale zero corretores")]
    public void EtapaComRegraNaoResolvida_RecusaSemPresumirNumero()
    {
        EtapaProcesso redacao = ComRecurso(NovaEtapa("Redação", 1, CorrecaoIndependente));
        ProcessoSeletivo processo = ProcessoComEtapas(null, redacao);
        redacao.DefinirBancas([DeRecursosDaEtapa(integrantes: 3, quorum: 2)]).IsSuccess.Should().BeTrue();

        DomainError? pendencia = Pendencia(processo, CorretoresDeTeste.SemConsolidacao);

        pendencia!.Code.Should().Be("EtapaProcesso.RegraDeConsolidacaoNaoResolvida");
        pendencia.Message.Should().Contain("'Redação'").And.NotContain(CorrecaoIndependente.Hash);
    }

    [Fact(DisplayName = "A etapa resolvida e a não resolvida julgadas pela mesma banca acumulam o mínimo e a regra não resolvida")]
    public void EtapaResolvidaENaoResolvida_AcumulamAsDuasRecusas()
    {
        EtapaProcesso redacao = ComRecurso(NovaEtapa("Redação", 1, CorrecaoIndependente));
        EtapaProcesso entrevista = ComRecurso(NovaEtapa("Entrevista", 2, AvaliacaoUnica));
        ProcessoSeletivo processo = ProcessoComEtapas([DeRecursosDaFase(integrantes: 2, quorum: 2)], redacao, entrevista);

        IReadOnlyList<FieldError> violacoes = Minimo(processo, Corretores((CorrecaoIndependente, 3)));

        violacoes.Select(static erro => erro.Error.Code).Should().Equal("Banca.IntegrantesAbaixoDoMinimo", "EtapaProcesso.RegraDeConsolidacaoNaoResolvida");
        violacoes[^1].Error.Message.Should().Contain("'Entrevista'");
    }

    [Fact(DisplayName = "A banca abaixo do mínimo vem antes da regra não resolvida de outra banca que a precede")]
    public void BancaAbaixoDoMinimo_VemAntesDaRegraNaoResolvidaDeBancaAnterior()
    {
        ReferenciaRegra lida = ReferenciaRegra.De(RegraConsolidacaoCodigo.CorrecaoIndependente, "v2", new string('f', 64));
        EtapaProcesso redacao = ComRecurso(NovaEtapa("Redação", 1, CorrecaoIndependente));
        EtapaProcesso entrevista = ComRecurso(NovaEtapa("Entrevista", 2, lida));
        ProcessoSeletivo processo = ProcessoComEtapas([DeRecursosDaFase(integrantes: 3, quorum: 2)], redacao, entrevista);
        entrevista.DefinirBancas([DeRecursosDaEtapa(integrantes: 2, quorum: 2)]).IsSuccess.Should().BeTrue();
        CorretoresDasEtapas corretores = Corretores((lida, 3));

        IEnumerable<string> violacoes = Minimo(processo, corretores).Select(static erro => erro.Error.Code);

        violacoes.Should().Equal("Banca.IntegrantesAbaixoDoMinimo", "EtapaProcesso.RegraDeConsolidacaoNaoResolvida");
        Pendencia(processo, corretores)!.Code.Should().Be("Banca.IntegrantesAbaixoDoMinimo");
        CodigosDaConferencia(processo, corretores).Should().Equal("Banca.IntegrantesAbaixoDoMinimo", "EtapaProcesso.RegraDeConsolidacaoNaoResolvida");
    }

    [Fact(DisplayName = "A etapa sem regra de consolidação julgada pela banca não gera recusa de regra não resolvida")]
    public void EtapaSemRegra_NaoGeraRecusaDeRegraNaoResolvida()
    {
        EtapaProcesso objetiva = ComRecurso(NovaEtapa("Objetiva", 1));
        ProcessoSeletivo processo = ProcessoComEtapas(null, objetiva);
        objetiva.DefinirBancas([DeRecursosDaEtapa(integrantes: 3, quorum: 2)]).IsSuccess.Should().BeTrue();

        Pendencia(processo, CorretoresDeTeste.SemConsolidacao).Should().BeNull();
    }

    [Fact(DisplayName = "O painel deixa vermelho o item do mínimo com a regra não resolvida, sem lançar")]
    public void Painel_ComRegraNaoResolvida_DeixaOItemVermelho()
    {
        EtapaProcesso redacao = ComRecurso(NovaEtapa("Redação", 1, CorrecaoIndependente));
        ProcessoSeletivo processo = ProcessoComEtapas(null, redacao);
        redacao.DefinirBancas([DeRecursosDaEtapa(integrantes: 3, quorum: 2)]).IsSuccess.Should().BeTrue();

        IEnumerable<string> vermelhos = Itens(processo, CorretoresDeTeste.SemConsolidacao)
            .Where(static item => !item.Ok).Select(static item => item.Codigo);

        vermelhos.Should().Equal("cronograma_banca_com_o_minimo_de_integrantes");
    }

    [Fact(DisplayName = "A conferência lê os corretores dos cadastros")]
    public void Conferencia_LeOsCorretoresDosCadastros()
    {
        EtapaProcesso redacao = ComRecurso(NovaEtapa("Redação", 1, CorrecaoIndependente));
        ProcessoSeletivo processo = ProcessoComEtapas(null, redacao);
        redacao.DefinirBancas([DeRecursosDaEtapa(integrantes: 2, quorum: 2)]).IsSuccess.Should().BeTrue();

        CodigosDaConferencia(processo, Corretores((CorrecaoIndependente, 2))).Should().BeEmpty();
        CodigosDaConferencia(processo, Corretores((CorrecaoIndependente, 3))).Should().Equal("Banca.IntegrantesAbaixoDoMinimo");
        CodigosDaConferencia(processo, CorretoresDeTeste.SemConsolidacao).Should().Equal("EtapaProcesso.RegraDeConsolidacaoNaoResolvida");
    }

    [Fact(DisplayName = "Redefinir as bancas da etapa leva à linha reaproveitada o nome e a regra declarados")]
    public void Redefinir_AlinhaNomeERegraDaLinhaReaproveitada()
    {
        EtapaProcesso etapa = NovaEtapa("Redação", 1);
        etapa.DefinirBancas([BancaDaEtapa.Criar(Tipo, "BANCA_EXAMINADORA", "Antigo", BancasDeTeste.Regra(3, 2))]).IsSuccess.Should().BeTrue();
        Guid linha = etapa.Bancas.Single().Id;

        Result definidas = AlinhamentoDasBancasDaEtapa.Definir(
            etapa, [BancaDaEtapa.Criar(Tipo, "BANCA_EXAMINADORA", "Novo", BancasDeTeste.Regra(5, 4))]);

        definidas.IsSuccess.Should().BeTrue();
        BancaDaEtapa banca = etapa.Bancas.Single();
        banca.Id.Should().Be(linha);
        banca.Nome.Should().Be("Novo");
        banca.RegraDeDeliberacao!.Integrantes.Should().Be(5);
    }
}
