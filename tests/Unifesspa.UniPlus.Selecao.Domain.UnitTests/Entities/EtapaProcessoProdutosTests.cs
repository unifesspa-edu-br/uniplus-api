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
        EtapaProcesso.Criar(
            new IdentificacaoDaEtapa(Nome: "Prova Objetiva", Carater: CaraterEtapa.Ambas, TipoEtapa: Tipo(), Ordem: null, FaseCodigo: null),
            new DeclaracaoDaAvaliacaoDaEtapa(Peso: 1m, NotaMinima: null, NotaMaxima: null, PassoDoLancamento: null, FormaDeAvaliacao: null, RegraConsolidacao: null, EscalaDeAtendimento: null)).Value!;

    private static EtapaProcesso EtapaCom(params ProdutoDaEtapa[] produtos)
    {
        EtapaProcesso etapa = Etapa();
        etapa.DefinirProdutos(produtos).IsSuccess.Should().BeTrue();
        return etapa;
    }

    private static ProdutoDaEtapa Preliminar(string ato) => ProdutoDaEtapa.Criar(ato, PapelProdutoFase.Preliminar);

    private static ProdutoDaEtapa Definitivo(string ato) => ProdutoDaEtapa.Criar(ato, PapelProdutoFase.Definitivo);

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
