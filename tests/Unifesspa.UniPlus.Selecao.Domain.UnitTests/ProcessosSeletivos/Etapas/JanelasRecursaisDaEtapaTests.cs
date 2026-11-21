namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Etapas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class JanelasRecursaisDaEtapaTests
{
    private static EtapaProcesso Etapa(bool notaDoEnem = false, params ProdutoDaEtapa[] produtos)
    {
        TipoEtapaSnapshot tipo = TipoEtapaSnapshot.Criar(
            Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva",
            admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: notaDoEnem).Value!;
        EtapaProcesso etapa = EtapaProcesso.Criar(
            new IdentificacaoDaEtapa("Prova Objetiva", CaraterEtapa.Ambas, tipo, Ordem: null, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(
                Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null,
                RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;
        etapa.DefinirProdutos(produtos).IsSuccess.Should().BeTrue();
        return etapa;
    }

    private static RecursoDaEtapa Janela(AncoraDoRecurso ancora, Guid produtoAncoraId, decimal horas = 48m) => RecursoDaEtapa.Criar(
        ancora,
        ReferenciaRegra.De(RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", new string('a', 64)),
        ParametrosDoRecursoDeTeste.Declarados(horas, UnidadePrazo.Horas),
        produtoAncoraId).Value!;

    private static ProdutoDaEtapa Preliminar(string ato) => ProdutoDaEtapa.Criar(ato, PapelProdutoFase.Preliminar);

    private static ProdutoDaEtapa Definitivo(string ato) => ProdutoDaEtapa.Criar(ato, PapelProdutoFase.Definitivo);

    [Fact(DisplayName = "A âncora no produto preliminar da etapa não é violação")]
    public void ViolacaoDaAncora_AtoPreliminarDaEtapa_NaoViola()
    {
        EtapaProcesso etapa = Etapa(false, Preliminar("GABARITO"));

        JanelasRecursaisDaEtapa.ViolacaoDaAncora(etapa, [Janela(AncoraDoRecurso.AtoPublicado, etapa.Produtos.Single().Id)])
            .Should().BeNull();
    }

    [Fact(DisplayName = "A âncora em produto definitivo da etapa é violação")]
    public void ViolacaoDaAncora_AtoDefinitivo_Viola()
    {
        EtapaProcesso etapa = Etapa(false, Definitivo("GABARITO"));

        DomainError? violacao = JanelasRecursaisDaEtapa.ViolacaoDaAncora(
            etapa, [Janela(AncoraDoRecurso.AtoPublicado, etapa.Produtos.Single().Id)]);

        violacao!.Code.Should().Be("EtapaProcesso.AncoraNaoEhProdutoPreliminarDaEtapa");
    }

    [Fact(DisplayName = "A âncora em produto que a etapa não publica é violação")]
    public void ViolacaoDaAncora_ProdutoDeOutraEtapa_Viola()
    {
        EtapaProcesso etapa = Etapa(false, Preliminar("GABARITO"));

        JanelasRecursaisDaEtapa.ViolacaoDaAncora(etapa, [Janela(AncoraDoRecurso.AtoPublicado, Guid.CreateVersion7())])!
            .Code.Should().Be("EtapaProcesso.AncoraNaoEhProdutoPreliminarDaEtapa");
    }

    [Fact(DisplayName = "A etapa de nota do ENEM não confere a âncora em ato: quem recusa é a regra de recurso da fase")]
    public void ViolacaoDaAncora_EtapaDoEnem_NaoConfereOAto()
    {
        EtapaProcesso etapa = Etapa(true, Definitivo("GABARITO"));

        JanelasRecursaisDaEtapa.ViolacaoDaAncora(etapa, [Janela(AncoraDoRecurso.AtoPublicado, Guid.CreateVersion7())])
            .Should().BeNull();
    }

    [Fact(DisplayName = "A âncora na ciência individual exige que a etapa prometa parecer individual")]
    public void ViolacaoDaAncora_CienciaSemParecerIndividual_Viola()
    {
        EtapaProcesso etapa = Etapa();

        JanelasRecursaisDaEtapa.ViolacaoDaAncora(etapa, [Janela(AncoraDoRecurso.CienciaIndividual, Guid.Empty)])!
            .Code.Should().Be("EtapaProcesso.CienciaSemParecerIndividual");
    }

    [Fact(DisplayName = "A âncora na ciência individual com parecer individual prometido não é violação")]
    public void ViolacaoDaAncora_CienciaComParecerIndividual_NaoViola()
    {
        EtapaProcesso etapa = Etapa();
        etapa.DefinirJanelaEParecer(null, null, emiteParecerIndividual: true).IsSuccess.Should().BeTrue();

        JanelasRecursaisDaEtapa.ViolacaoDaAncora(etapa, [Janela(AncoraDoRecurso.CienciaIndividual, Guid.Empty)])
            .Should().BeNull();
    }

    [Fact(DisplayName = "Sem janelas não há âncora a violar")]
    public void ViolacaoDaAncora_SemJanelas_NaoViola()
    {
        JanelasRecursaisDaEtapa.ViolacaoDaAncora(Etapa(), []).Should().BeNull();
    }

    [Fact(DisplayName = "Duas janelas para a mesma publicação são o mesmo fato repetido")]
    public void FatoRepetido_DuasJanelasParaOMesmoAto_Recusa()
    {
        Guid produto = Guid.CreateVersion7();

        DomainError? recusa = JanelasRecursaisDaEtapa.FatoRepetido(
            [Janela(AncoraDoRecurso.AtoPublicado, produto), Janela(AncoraDoRecurso.AtoPublicado, produto, 24m)]);

        recusa!.Code.Should().Be("EtapaProcesso.RecursoDuplicadoNaEtapa");
    }

    [Fact(DisplayName = "Duas janelas para a ciência são o mesmo fato repetido")]
    public void FatoRepetido_DuasJanelasParaACiencia_Recusa()
    {
        DomainError? recusa = JanelasRecursaisDaEtapa.FatoRepetido(
            [Janela(AncoraDoRecurso.CienciaIndividual, Guid.Empty), Janela(AncoraDoRecurso.CienciaIndividual, Guid.Empty)]);

        recusa!.Code.Should().Be("EtapaProcesso.RecursoDuplicadoNaEtapa");
    }

    [Fact(DisplayName = "Publicações diferentes e a ciência são fatos diferentes")]
    public void FatoRepetido_FatosDiferentes_NaoRecusa()
    {
        JanelasRecursaisDaEtapa.FatoRepetido(
        [
            Janela(AncoraDoRecurso.AtoPublicado, Guid.CreateVersion7()),
            Janela(AncoraDoRecurso.AtoPublicado, Guid.CreateVersion7()),
            Janela(AncoraDoRecurso.CienciaIndividual, Guid.Empty),
        ]).Should().BeNull();
    }
}
