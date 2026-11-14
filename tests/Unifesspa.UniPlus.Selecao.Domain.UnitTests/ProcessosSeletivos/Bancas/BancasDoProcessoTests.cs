namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Bancas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

/// <summary>
/// A exigência de banca de recursos e o mínimo de integrantes, sobre o estado do processo: a mesma
/// pendência alimenta o gate de congelamento e o painel.
/// </summary>
public sealed class BancasDoProcessoTests
{
    private static readonly Guid Tipo = new("0197d6f0-0000-7000-8000-00000000b4c1");

    private static RegraRecursoFase RegraDeRecurso() => RegraRecursoFase.Criar(
        ReferenciaRegra.Criar(RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", new string('c', 64)).Value!,
        new ArgsRegraPrazoRecurso(2m, UnidadePrazo.DiasUteis, null, null, false, null, null),
        ProcessoConformeFactory.ProdutoPreliminarId).Value!;

    private static EtapaProcesso NovaEtapa(string nome, int ordem) => EtapaProcesso.Criar(
        nome, CaraterEtapa.Classificatoria,
        TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!,
        peso: 1m, ordem: ordem, notaMaxima: 10m, formaDeAvaliacao: FormaDeAvaliacao.Objetiva, faseCodigo: "RESULTADO_FINAL").Value!;

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

    private static CorretoresDasEtapas Corretores(params (EtapaProcesso Etapa, int Numero)[] porEtapa) =>
        new(porEtapa.ToDictionary(static par => par.Etapa.Id, static par => par.Numero));

    private static DomainError? Pendencia(ProcessoSeletivo processo, CorretoresDasEtapas? corretores = null) =>
        PendenciasDasBancas.PendenciaDasBancas(EstadoDaConfiguracao.De(processo), corretores ?? CorretoresDasEtapas.Nenhum);

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

    [Fact(DisplayName = "Banca de análise de recursos com recorte de competência é recusada")]
    public void BancaDeRecursosComRecorte_Recusa()
    {
        BancaRequerida comRecorte = BancaRequerida.Criar(
            Tipo, TipoBancaCodigo.AnaliseDeRecursos, "Banca de análise de recursos",
            [CategoriaJulgada.Criar(Guid.CreateVersion7(), "RENDA")], BancasDeTeste.Regra());
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar(
            fase: ProcessoConformeFactory.FaseConforme(RegraDeRecurso(), bancas: [comRecorte]));

        Pendencia(processo)!.Code.Should().Be("Banca.RecorteEmBancaDeRecursos");
        ExigenciaDeBancaDeRecurso.RecorteNaDeclaracao(TipoBancaCodigo.AnaliseDeRecursos, 1)!.Code
            .Should().Be("Banca.RecorteEmBancaDeRecursos");
        ExigenciaDeBancaDeRecurso.RecorteNaDeclaracao("BANCA_ANALISE_DOCUMENTAL", 1).Should().BeNull();
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
        EtapaProcesso redacao = ComRecurso(NovaEtapa("Redação", 1));
        ProcessoSeletivo processo = ProcessoComEtapas(null, redacao);
        redacao.DefinirBancas([DeRecursosDaEtapa(integrantes: 2, quorum: 2)]).IsSuccess.Should().BeTrue();

        DomainError? pendencia = Pendencia(processo, Corretores((redacao, 3)));

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
        EtapaProcesso etapa = ComRecurso(NovaEtapa("Redação", 1));
        ProcessoSeletivo processo = ProcessoComEtapas(null, etapa);
        etapa.DefinirBancas([DeRecursosDaEtapa(integrantes, quorum)]).IsSuccess.Should().BeTrue();

        Pendencia(processo, Corretores((etapa, corretores))).Should().BeNull();
    }

    [Fact(DisplayName = "Quórum maior que os corretores eleva o mínimo ao quórum")]
    public void Minimo_ComQuorumMaiorQueOsCorretores_ExigeOQuorum()
    {
        MinimoDeIntegrantes.Exigido(BancasDeTeste.Regra(integrantes: 5, quorum: 4), [], CorretoresDasEtapas.Nenhum)
            .Should().Be(4);
    }

    [Fact(DisplayName = "Banca da fase que julga duas etapas usa o maior número de corretores entre elas")]
    public void BancaDaFase_JulgandoDuasEtapas_UsaOMaiorNumeroDeCorretores()
    {
        EtapaProcesso redacao = ComRecurso(NovaEtapa("Redação", 1));
        EtapaProcesso objetiva = ComRecurso(NovaEtapa("Objetiva", 2));
        ProcessoSeletivo processo = ProcessoComEtapas([DeRecursosDaFase(integrantes: 2, quorum: 2)], redacao, objetiva);

        DomainError? pendencia = Pendencia(processo, Corretores((redacao, 3), (objetiva, 0)));

        pendencia!.Code.Should().Be("Banca.IntegrantesAbaixoDoMinimo");
        pendencia.Message.Should().Contain("o mínimo exigido é 3");
    }

    [Fact(DisplayName = "Banca da etapa julga só a própria etapa, mesmo que outra exija mais corretores")]
    public void BancaDaEtapa_JulgaSoAPropriaEtapa()
    {
        EtapaProcesso redacao = ComRecurso(NovaEtapa("Redação", 1));
        EtapaProcesso objetiva = ComRecurso(NovaEtapa("Objetiva", 2));
        ProcessoSeletivo processo = ProcessoComEtapas([DeRecursosDaFase(integrantes: 3, quorum: 2)], redacao, objetiva);
        processo.Etapas.Single(e => e.Nome == "Objetiva").DefinirBancas([DeRecursosDaEtapa(integrantes: 1, quorum: 1)]).IsSuccess.Should().BeTrue();

        Pendencia(processo, Corretores((redacao, 3), (objetiva, 1))).Should().BeNull();
    }

    [Fact(DisplayName = "Banca da etapa que não é de recursos não julga a etapa e não herda o mínimo de corretores")]
    public void BancaDaEtapaQueNaoEDeRecursos_NaoHerdaOMinimoDeCorretores()
    {
        EtapaProcesso redacao = ComRecurso(NovaEtapa("Redação", 1));
        ProcessoSeletivo processo = ProcessoComEtapas([BancasDeTeste.DeRecursosDaFase()], redacao);
        redacao.DefinirBancas([BancaDaEtapa.Criar(Tipo, "BANCA_EXAMINADORA", "Banca examinadora", BancasDeTeste.Regra(integrantes: 2, quorum: 2))])
            .IsSuccess.Should().BeTrue();

        Pendencia(processo, Corretores((redacao, 3))).Should().BeNull();
    }

    [Fact(DisplayName = "O painel projeta a mesma pendência do gate em cada item das bancas")]
    public void Painel_ProjetaAPendenciaDoGate()
    {
        EtapaProcesso redacao = ComRecurso(NovaEtapa("Redação", 1));
        ProcessoSeletivo processo = ProcessoComEtapas(null, redacao);
        redacao.DefinirBancas([DeRecursosDaEtapa(integrantes: 2, quorum: 2)]).IsSuccess.Should().BeTrue();
        CorretoresDasEtapas corretores = Corretores((redacao, 3));

        IEnumerable<string> vermelhos = ItensDasBancas.Avaliar(EstadoDaConfiguracao.De(processo), corretores)
            .Where(static item => !item.Ok).Select(static item => item.Codigo);

        vermelhos.Should().Equal("cronograma_banca_com_o_minimo_de_integrantes");
        ItensDasBancas.Avaliar(EstadoDaConfiguracao.De(processo), CorretoresDasEtapas.Nenhum).Should().OnlyContain(static item => item.Ok);
    }

    private static PedidoDeCongelamento Pedido(CorretoresDasEtapas corretores) => new()
    {
        Dados = ProcessoConformeFactory.Dados(),
        Documento = new DocumentoCongelado([], "1.1", "canonical-json/sha256@v1", new string('a', 64)),
        AtorUsuarioSub = "teste",
        Instante = TimeProvider.System.GetUtcNow(),
        Contexto = new ContextoDeContagemDePrazos(null, FusoInstitucional: TimeZoneInfo.Utc),
        FatosDeModalidade = FatosDeModalidadeDeTeste.DoCatalogo,
        TiposDeAtoUnicos = TiposDeAtoUnicosDeTeste.Nenhum,
        Corretores = corretores,
    };

    [Fact(DisplayName = "O portão do cronograma lê os corretores do pedido de congelamento")]
    public void PortaoDoCronograma_LeOsCorretoresDoPedido()
    {
        EtapaProcesso redacao = ComRecurso(NovaEtapa("Redação", 1));
        ProcessoSeletivo processo = ProcessoComEtapas(null, redacao);
        redacao.DefinirBancas([DeRecursosDaEtapa(integrantes: 2, quorum: 2)]).IsSuccess.Should().BeTrue();
        EstadoDaConfiguracao estado = EstadoDaConfiguracao.De(processo);

        ProntidaoParaCongelar.Portoes.Para(estado, Pedido(CorretoresDasEtapas.Nenhum)).Cronograma().Should().BeNull();
        ProntidaoParaCongelar.Portoes.Para(estado, Pedido(Corretores((redacao, 3)))).Cronograma()!.Code
            .Should().Be("Banca.IntegrantesAbaixoDoMinimo");
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
