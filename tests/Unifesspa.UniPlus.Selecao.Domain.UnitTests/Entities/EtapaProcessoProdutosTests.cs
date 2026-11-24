namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Xunit;

/// <summary>
/// Caracterização do comportamento de <see cref="EtapaProcesso"/> na declaração dos produtos
/// (<c>DefinirProdutos</c>): a reconciliação das linhas vivas sob a normalização Unicode do código
/// e a identidade do que continua declarado.
/// </summary>
public sealed class EtapaProcessoProdutosTests
{
    private const string AtoComposto = "AVALIAÇÃO_FINAL";
    private const string AtoDecomposto = "AVALIAÇÃO_FINAL";

    private static TipoEtapaSnapshot Tipo() => TipoEtapaSnapshot.Criar(
        Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva",
        admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!;

    private static EtapaProcesso Etapa() =>
        EtapaProcesso.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("etapa").Value!,
            new IdentificacaoDaEtapa(Nome: "Prova Objetiva", Carater: CaraterEtapa.Ambas, TipoEtapa: Tipo(), Ordem: 1, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;

    private static EtapaProcesso EtapaCom(params ProdutoDaEtapa[] produtos)
    {
        EtapaProcesso etapa = Etapa();
        etapa.DefinirProdutos(produtos).IsSuccess.Should().BeTrue();
        return etapa;
    }

    private static ProdutoDaEtapa Preliminar(string ato) => ProdutoDaEtapa.Criar(ato, PapelDaPublicacaoPrevista.Preliminar);

    private static ProdutoDaEtapa Definitivo(string ato) => ProdutoDaEtapa.Criar(ato, PapelDaPublicacaoPrevista.Definitivo);

    [Fact(DisplayName = "DefinirProdutos recria o produto que trocou de papel, em uma passada só")]
    public void DefinirProdutos_ProdutoQueTrocouDePapel_RecriaALinha()
    {
        EtapaProcesso etapa = EtapaCom(Preliminar("GABARITO"));
        Guid idAntigo = etapa.Produtos.Single().Id;

        etapa.DefinirProdutos([Definitivo("GABARITO")]).IsSuccess.Should().BeTrue();

        ProdutoDaEtapa produto = etapa.Produtos.Should().ContainSingle().Subject;
        produto.Id.Should().NotBe(idAntigo);
        produto.Papel.Should().Be(PapelDaPublicacaoPrevista.Definitivo);
    }

    [Fact(DisplayName = "DefinirProdutos casa o ato em forma de acento diferente e preserva o Id")]
    public void DefinirProdutos_AtoEmFormaDeAcentoDiferente_PreservaOId()
    {
        EtapaProcesso etapa = EtapaCom(Preliminar(AtoComposto));
        Guid idDaLinha = etapa.Produtos.Single().Id;

        etapa.DefinirProdutos([Preliminar(AtoDecomposto)]).IsSuccess.Should().BeTrue();

        etapa.Produtos.Should().ContainSingle().Which.Id.Should().Be(idDaLinha);
    }

    [Fact(DisplayName = "DefinirProdutos recusa o mesmo ato e papel escrito em formas de acento diferentes, no produto repetido")]
    public void DefinirProdutos_AtoRepetidoEmFormasDeAcentoDiferentes_RecusaNoProdutoRepetido()
    {
        Result resultado = Etapa().DefinirProdutos([Preliminar(AtoComposto), Preliminar(AtoDecomposto)]);

        FieldError recusa = resultado.Errors.Should().ContainSingle().Subject;
        recusa.Field.Should().Be("produtos[1]");
        recusa.Error.Code.Should().Be("EtapaProcesso.AtoDuplicadoNaEtapa");
        recusa.Error.Message.Should().Contain($"'{AtoDecomposto}'");
    }

    [Fact(DisplayName = "O par repetido A, B, B, A recusa o segundo B e o segundo A, cada um no seu produto")]
    public void DefinirProdutos_ParRepetidoEmAbba_RecusaCadaRepeticao()
    {
        Result resultado = Etapa().DefinirProdutos(
            [Preliminar("ATO_A"), Preliminar("ATO_B"), Preliminar("ATO_B"), Preliminar("ATO_A")]);

        resultado.Errors.Select(static e => e.Field).Should().Equal("produtos[2]", "produtos[3]");
        resultado.Errors.Select(static e => e.Error.Code).Should().AllBe("EtapaProcesso.AtoDuplicadoNaEtapa");
        resultado.Errors[0].Error.Message.Should().Contain("'ATO_B'");
        resultado.Errors[1].Error.Message.Should().Contain("'ATO_A'");
    }

    [Fact(DisplayName = "O par repetido A, B, A recusa só o terceiro produto, e o primeiro A segue valendo")]
    public void DefinirProdutos_ParRepetidoEmAba_RecusaOTerceiroProduto()
    {
        Result resultado = Etapa().DefinirProdutos([Preliminar("ATO_A"), Preliminar("ATO_B"), Preliminar("ATO_A")]);

        FieldError recusa = resultado.Errors.Should().ContainSingle().Subject;
        recusa.Field.Should().Be("produtos[2]");
        recusa.Error.Code.Should().Be("EtapaProcesso.AtoDuplicadoNaEtapa");
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
