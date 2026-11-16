namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Xunit;

/// <summary>
/// Caracterização do comportamento de <see cref="EtapaProcesso"/> na reposição da versão
/// congelada (<c>ReporDe</c>) e na declaração dos produtos (<c>DefinirProdutos</c>): a
/// reconciliação das linhas vivas sob a normalização Unicode do código e a identidade do que
/// continua declarado.
/// </summary>
public sealed class EtapaProcessoReposicaoTests
{
    private const string AtoComposto = "AVALIAÇÃO_FINAL";
    private const string AtoDecomposto = "AVALIAÇÃO_FINAL";
    private const string BancaComposta = "BANCA_HETEROIDENTIFICAÇÃO";
    private const string BancaDecomposta = "BANCA_HETEROIDENTIFICAÇÃO";
    private static readonly DateTimeOffset InicioDaJanela = new(2027, 1, 25, 11, 0, 0, TimeSpan.Zero);

    private static TipoEtapaSnapshot Tipo() => TipoEtapaSnapshot.Criar(
        Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva",
        admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!;

    private static EtapaProcesso Etapa() =>
        EtapaProcesso.Criar(
            new IdentificacaoDaEtapa(Nome: "Prova Objetiva", Carater: CaraterEtapa.Ambas, TipoEtapa: Tipo(), Ordem: null, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;

    private static EtapaProcesso EtapaCom(params ProdutoDaEtapa[] produtos)
    {
        EtapaProcesso etapa = Etapa();
        etapa.DefinirProdutos(produtos).IsSuccess.Should().BeTrue();
        return etapa;
    }

    private static RecursoDaEtapa Janela(Guid produtoAncoraId) => RecursoDaEtapa.Criar(
        AncoraDoRecurso.AtoPublicado,
        ReferenciaRegra.De(RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", new string('a', 64)),
        new ArgsRegraPrazoRecurso(48m, UnidadePrazo.Horas, null, null, false, null, null),
        produtoAncoraId).Value!;

    private static EtapaProcesso EtapaComJanelaSobreOPreliminarDe(string ato)
    {
        EtapaProcesso etapa = EtapaCom(Preliminar(ato));
        etapa.DefinirRecursos([Janela(etapa.Produtos.Single().Id)]).IsSuccess.Should().BeTrue();
        return etapa;
    }

    private static ProdutoDaEtapa Preliminar(string ato) => ProdutoDaEtapa.Criar(ato, PapelProdutoFase.Preliminar);

    private static ProdutoDaEtapa Definitivo(string ato) => ProdutoDaEtapa.Criar(ato, PapelProdutoFase.Definitivo);

    private static EtapaProcesso EtapaComBanca(Guid tipoBanca, string codigo, string nome)
    {
        EtapaProcesso etapa = Etapa();
        etapa.DefinirBancas([BancaDaEtapa.Criar(tipoBanca, codigo, nome)]).IsSuccess.Should().BeTrue();
        return etapa;
    }

    // ── ReporDe: produtos ──

    [Fact(DisplayName = "ReporDe casa o ato com acento decomposto com a linha viva em forma composta")]
    public void ReporDe_AtoEmFormaDeAcentoDiferente_CasaComALinhaViva()
    {
        AtoDecomposto.IsNormalized(System.Text.NormalizationForm.FormC).Should().BeFalse();
        EtapaProcesso viva = EtapaCom(Preliminar(AtoComposto));
        Guid idDaLinha = viva.Produtos.Single().Id;

        viva.ReporDe(EtapaCom(Preliminar(AtoDecomposto)));

        viva.Produtos.Should().ContainSingle().Which.Id.Should().Be(idDaLinha);
    }

    [Fact(DisplayName = "ReporDe casa pelo ato normalizado e mantém o texto da linha viva")]
    public void ReporDe_AtoCasadoPelaNormalizacao_MantemOTextoDaLinhaViva()
    {
        EtapaProcesso viva = EtapaCom(Preliminar(AtoDecomposto));

        viva.ReporDe(EtapaCom(Preliminar(AtoComposto)));

        viva.Produtos.Single().AtoCodigo.Should().Be(AtoDecomposto);
    }

    [Fact(DisplayName = "ReporDe preserva o Id do produto que só trocou de papel")]
    public void ReporDe_ProdutoQueTrocouDePapel_PreservaOId()
    {
        EtapaProcesso viva = EtapaCom(Preliminar("GABARITO"));
        Guid idDaLinha = viva.Produtos.Single().Id;

        viva.ReporDe(EtapaCom(Definitivo("GABARITO")));

        ProdutoDaEtapa produto = viva.Produtos.Should().ContainSingle().Subject;
        produto.Id.Should().Be(idDaLinha);
        produto.Papel.Should().Be(PapelProdutoFase.Definitivo);
        produto.EtapaProcessoId.Should().Be(viva.Id);
    }

    [Fact(DisplayName = "ReporDe consome cada linha viva uma vez: preliminar e definitiva da matéria não casam com a mesma")]
    public void ReporDe_MesmaMateriaComDoisPapeis_ConsomeACadaLinhaUmaVez()
    {
        EtapaProcesso viva = EtapaCom(Preliminar("GABARITO"));
        Guid idPreliminarVivo = viva.Produtos.Single().Id;
        EtapaProcesso congelada = EtapaCom(Definitivo("GABARITO"), Preliminar("GABARITO"));
        Guid idDefinitivoCongelado = congelada.Produtos.Single(static p => p.Papel == PapelProdutoFase.Definitivo).Id;

        viva.ReporDe(congelada);

        viva.Produtos.Select(static p => p.Papel).Should().Equal(PapelProdutoFase.Definitivo, PapelProdutoFase.Preliminar);
        viva.Produtos.Select(static p => p.Id).Should().Equal(idDefinitivoCongelado, idPreliminarVivo);
    }

    // ── ReporDe: janela recursal ──

    [Fact(DisplayName = "ReporDe mantém a janela recursal viva quando a âncora traduzida é a mesma")]
    public void ReporDe_JanelaComAncoraTraduzida_MantemAInstanciaViva()
    {
        EtapaProcesso viva = EtapaComJanelaSobreOPreliminarDe(AtoDecomposto);
        RecursoDaEtapa janelaViva = viva.Recursos.Single();
        Guid idDoProdutoVivo = viva.Produtos.Single().Id;

        viva.ReporDe(EtapaComJanelaSobreOPreliminarDe(AtoComposto));

        viva.Recursos.Should().ContainSingle().Which.Should().BeSameAs(janelaViva);
        janelaViva.ProdutoAncoraId.Should().Be(idDoProdutoVivo);
    }

    [Fact(DisplayName = "ReporDe traduz a âncora para a linha viva que trocou de papel")]
    public void ReporDe_AncoraEmLinhaQueTrocouDePapel_ApontaParaALinhaViva()
    {
        EtapaProcesso viva = EtapaCom(Definitivo("GABARITO"));
        Guid idDaLinhaViva = viva.Produtos.Single().Id;
        EtapaProcesso congelada = EtapaComJanelaSobreOPreliminarDe("GABARITO");

        viva.ReporDe(congelada);

        RecursoDaEtapa janela = viva.Recursos.Should().ContainSingle().Subject;
        janela.Should().BeSameAs(congelada.Recursos.Single());
        janela.ProdutoAncoraId.Should().Be(idDaLinhaViva);
        janela.EtapaProcessoId.Should().Be(viva.Id);
        JanelasRecursaisDaEtapa.ViolacaoDaAncora(viva, viva.Recursos).Should().BeNull();
    }

    // ── ReporDe: bancas ──

    [Fact(DisplayName = "ReporDe reaproveita a banca de código equivalente em NFC e repõe o tipo de origem")]
    public void ReporDe_BancaComCodigoEmNfc_ReaproveitaALinhaEReporAOrigem()
    {
        Guid tipoCongelado = Guid.CreateVersion7();
        EtapaProcesso viva = EtapaComBanca(Guid.CreateVersion7(), BancaDecomposta, "Banca viva");
        Guid idDaBanca = viva.Bancas.Single().Id;

        viva.ReporDe(EtapaComBanca(tipoCongelado, BancaComposta, "Banca congelada"));

        BancaDaEtapa banca = viva.Bancas.Should().ContainSingle().Subject;
        banca.Id.Should().Be(idDaBanca);
        banca.TipoBancaOrigemId.Should().Be(tipoCongelado);
        banca.EtapaProcessoId.Should().Be(viva.Id);
    }

    [Fact(DisplayName = "ReporDe não reescreve nome nem código da banca reaproveitada")]
    public void ReporDe_BancaReaproveitada_MantemNomeECodigoDaLinhaViva()
    {
        EtapaProcesso viva = EtapaComBanca(Guid.CreateVersion7(), BancaDecomposta, "Banca viva");

        viva.ReporDe(EtapaComBanca(Guid.CreateVersion7(), BancaComposta, "Banca congelada"));

        BancaDaEtapa banca = viva.Bancas.Single();
        (banca.Nome, banca.Codigo).Should().Be(("Banca viva", BancaDecomposta));
    }

    // ── ReporDe: dados da etapa ──

    [Fact(DisplayName = "ReporDe repõe a janela própria em UTC, o parecer e os dados da etapa congelada")]
    public void ReporDe_DadosDaEtapaCongelada_SaoReplicadosNaViva()
    {
        EtapaProcesso congelada = EtapaProcesso.Criar(
            new IdentificacaoDaEtapa(Nome: "  Entrevista  ", Carater: CaraterEtapa.Eliminatoria, TipoEtapa: Tipo(), Ordem: 2, FaseCodigo: " AVALIACAO "),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: null, NotaMinima: 5m, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;
        congelada.DefinirJanelaEParecer(
            InicioDaJanela.ToOffset(TimeSpan.FromHours(-3)), InicioDaJanela.AddDays(1), emiteParecerIndividual: true)
            .IsSuccess.Should().BeTrue();
        EtapaProcesso viva = Etapa();

        viva.ReporDe(congelada);

        (viva.Nome, viva.Carater, viva.Peso, viva.NotaMinima, viva.Ordem, viva.FaseCodigo, viva.EmiteParecerIndividual)
            .Should().Be(("Entrevista", CaraterEtapa.Eliminatoria, null, 5m, 2, "AVALIACAO", true));
        viva.Inicio.Should().Be(InicioDaJanela);
        viva.Inicio!.Value.Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact(DisplayName = "ReporDe não revalida a forma: repõe peso em etapa só eliminatória, como veio congelada")]
    public void ReporDe_EtapaCongeladaForaDaFormaAtual_ERepostaSemRecusa()
    {
        EtapaProcesso congelada = EtapaProcesso.Reidratar(
            Guid.CreateVersion7(),
            new IdentificacaoDaEtapa(Nome: "Entrevista", Carater: CaraterEtapa.Eliminatoria, TipoEtapa: Tipo(), Ordem: 1, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 2m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null),
            itens: null);
        EtapaProcesso viva = Etapa();

        viva.ReporDe(congelada);

        (viva.Carater, viva.Peso).Should().Be((CaraterEtapa.Eliminatoria, 2m));
    }

    // ── DefinirProdutos ──

    [Fact(DisplayName = "DefinirProdutos recria o produto que trocou de papel, em uma passada só")]
    public void DefinirProdutos_ProdutoQueTrocouDePapel_RecriaALinha()
    {
        EtapaProcesso etapa = EtapaCom(Preliminar("GABARITO"));
        Guid idAntigo = etapa.Produtos.Single().Id;

        etapa.DefinirProdutos([Definitivo("GABARITO")]).IsSuccess.Should().BeTrue();

        ProdutoDaEtapa produto = etapa.Produtos.Should().ContainSingle().Subject;
        produto.Id.Should().NotBe(idAntigo);
        produto.Papel.Should().Be(PapelProdutoFase.Definitivo);
    }

    [Fact(DisplayName = "DefinirProdutos casa o ato em forma de acento diferente e preserva o Id")]
    public void DefinirProdutos_AtoEmFormaDeAcentoDiferente_PreservaOId()
    {
        EtapaProcesso etapa = EtapaCom(Preliminar(AtoComposto));
        Guid idDaLinha = etapa.Produtos.Single().Id;

        etapa.DefinirProdutos([Preliminar(AtoDecomposto)]).IsSuccess.Should().BeTrue();

        etapa.Produtos.Should().ContainSingle().Which.Id.Should().Be(idDaLinha);
    }

    [Fact(DisplayName = "DefinirProdutos recusa o mesmo ato e papel escrito em formas de acento diferentes")]
    public void DefinirProdutos_AtoRepetidoEmFormasDeAcentoDiferentes_Recusa()
    {
        Result resultado = Etapa().DefinirProdutos([Preliminar(AtoComposto), Preliminar(AtoDecomposto)]);

        resultado.Error!.Code.Should().Be("EtapaProcesso.AtoDuplicadoNaEtapa");
        resultado.Error.Message.Should().Contain($"'{AtoComposto}'");
    }

    [Fact(DisplayName = "O par repetido A, B, B, A informa o ato A, a primeira ocorrência repetida")]
    public void DefinirProdutos_ParRepetidoEmAbba_InformaAPrimeiraOcorrencia()
    {
        Result resultado = Etapa().DefinirProdutos(
            [Preliminar("ATO_A"), Preliminar("ATO_B"), Preliminar("ATO_B"), Preliminar("ATO_A")]);

        resultado.Error!.Code.Should().Be("EtapaProcesso.AtoDuplicadoNaEtapa");
        resultado.Error.Message.Should().Contain("'ATO_A'").And.NotContain("'ATO_B'");
    }

    [Fact(DisplayName = "DefinirProdutos recusado deixa os produtos da etapa como estavam")]
    public void DefinirProdutos_Recusado_NaoAlteraOsProdutos()
    {
        EtapaProcesso etapa = EtapaCom(Preliminar("GABARITO"));
        Guid idDaLinha = etapa.Produtos.Single().Id;

        etapa.DefinirProdutos([Preliminar("ATO_A"), Preliminar("ATO_A")]).IsFailure.Should().BeTrue();

        etapa.Produtos.Should().ContainSingle().Which.Id.Should().Be(idDaLinha);
    }
}
