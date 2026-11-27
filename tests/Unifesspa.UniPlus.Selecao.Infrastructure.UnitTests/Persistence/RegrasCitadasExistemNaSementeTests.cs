namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Persistence;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Seed;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class RegrasCitadasExistemNaSementeTests
{
    private const string Citada = "REGRA_EXEMPLO";
    private const string VersaoCitada = "v1";

    public static TheoryData<string> Envelopes => new(RegrasCitadasNosEnvelopesDeReferencia.Arquivos);

    [Theory]
    [MemberData(nameof(Envelopes))]
    public void EnvelopeDeReferencia_CadaRegraCitada_ExisteNaSementeDoCatalogo(string arquivo)
    {
        IReadOnlyList<(string Codigo, string Versao)> citadas = RegrasCitadasNosEnvelopesDeReferencia.DoArquivo(arquivo);

        IReadOnlyList<(string Codigo, string Versao)> ausentes =
            ConferenciaDasRegrasCitadasNaSemente.Ausentes(citadas, ParesDaSemente(RegraCatalogoSeed.Itens));

        citadas.Should().NotBeEmpty("o envelope de referência cita regras do catálogo");
        RegrasCitadasNosEnvelopesDeReferencia.ObjetosComVersaoQueNaoSaoReferencia(JsonNode.Parse(EnvelopesDeReferenciaDeTeste.Ler(arquivo))!)
            .Should().Be(0, "todo objeto com a chave versao deve ser lido como referência de regra");
        ausentes.Should().BeEmpty("{0}", ConferenciaDasRegrasCitadasNaSemente.Descrever(ausentes));
    }

    [Fact]
    public void Leitor_ReferenciaEmBlocoAninhadoEmLista_EEncontrada()
    {
        JsonNode envelope = JsonNode.Parse(
            """{"bloco":{"itens":[{"regra":{"codigo":"REGRA_EXEMPLO","hash":"00","versao":"v1"}},{"nome":"sem referência"}]}}""")!;

        RegrasCitadasNosEnvelopesDeReferencia.Do(envelope).Should().Equal([(Citada, VersaoCitada)]);
    }

    [Fact]
    public void Leitor_ReferenciaDentroDeOutraReferencia_AsDuasSaoEncontradas()
    {
        JsonNode envelope = JsonNode.Parse(
            """{"regra":{"codigo":"REGRA_EXEMPLO","versao":"v1","interna":{"codigo":"REGRA_EXEMPLO_ANTIGA","versao":"v2"}}}""")!;

        RegrasCitadasNosEnvelopesDeReferencia.Do(envelope).Should().BeEquivalentTo([(Citada, VersaoCitada), ("REGRA_EXEMPLO_ANTIGA", "v2")]);
    }

    [Theory]
    [InlineData("""{"x":{"versao":"v1"}}""")]
    [InlineData("""{"x":{"codigo":7,"versao":"v1"}}""")]
    [InlineData("""{"x":[{"codigo":"REGRA_EXEMPLO","versao":1}]}""")]
    public void Guarda_ObjetoComVersaoQueNaoELidoComoReferencia_EContado(string json) =>
        RegrasCitadasNosEnvelopesDeReferencia.ObjetosComVersaoQueNaoSaoReferencia(JsonNode.Parse(json)!).Should().Be(1);

    [Fact]
    public void Leitor_MesmaReferenciaCitadaDuasVezes_ContaUmaVez()
    {
        JsonNode envelope = JsonNode.Parse(
            """{"a":{"codigo":"REGRA_EXEMPLO","versao":"v1"},"b":{"codigo":"REGRA_EXEMPLO","versao":"v1"}}""")!;

        RegrasCitadasNosEnvelopesDeReferencia.Do(envelope).Should().ContainSingle();
    }

    [Fact]
    public void Ausentes_SementeSemARegraCitada_InformaCodigoEVersao()
    {
        IReadOnlyList<(string Codigo, string Versao)> ausentes =
            ConferenciaDasRegrasCitadasNaSemente.Ausentes([(Citada, VersaoCitada)], ParesDaSemente([Item("OUTRA_REGRA", "v1")]));

        ausentes.Should().Equal([(Citada, VersaoCitada)]);
        ConferenciaDasRegrasCitadasNaSemente.Descrever(ausentes).Should().Contain("código 'REGRA_EXEMPLO', versão 'v1'");
    }

    [Fact]
    public void Ausentes_SementeSoComOutraVersaoDaRegra_ApontaAVersaoCitada()
    {
        IReadOnlyList<(string Codigo, string Versao)> ausentes =
            ConferenciaDasRegrasCitadasNaSemente.Ausentes([(Citada, VersaoCitada)], ParesDaSemente([Item(Citada, "v2")]));

        ausentes.Should().Equal([(Citada, VersaoCitada)]);
    }

    [Fact]
    public void Ausentes_SementeSoComCodigoQueComecaIgual_NaoConfundeOsCodigos()
    {
        IReadOnlyList<(string Codigo, string Versao)> ausentes =
            ConferenciaDasRegrasCitadasNaSemente.Ausentes([(Citada, VersaoCitada)], ParesDaSemente([Item("REGRA_EXEMPLO_ANTIGA", VersaoCitada)]));

        ausentes.Should().Equal([(Citada, VersaoCitada)]);
    }

    [Fact]
    public void Ausentes_CodigoComDiferencaDeCaixa_NaoContaComoOMesmo()
    {
        ConferenciaDasRegrasCitadasNaSemente.Ausentes([(Citada, VersaoCitada)], ParesDaSemente([Item("regra_exemplo", VersaoCitada)]))
            .Should().ContainSingle();
    }

    [Fact]
    public void Ausentes_SementeComOParExato_NaoApontaNada()
    {
        ConferenciaDasRegrasCitadasNaSemente.Ausentes(
            [(Citada, VersaoCitada)], ParesDaSemente([Item(Citada, "v2"), Item(Citada, VersaoCitada)])).Should().BeEmpty();
    }

    [Fact]
    public void Ausentes_VariasFaltando_ListaCadaParUmaVezEmOrdemEstavel()
    {
        IReadOnlyList<(string Codigo, string Versao)> ausentes = ConferenciaDasRegrasCitadasNaSemente.Ausentes(
            [("B_REGRA", "v1"), ("A_REGRA", "v1"), ("B_REGRA", "v1")], ParesDaSemente([]));

        ausentes.Should().Equal([("A_REGRA", "v1"), ("B_REGRA", "v1")]);
    }

    private static IEnumerable<(string Codigo, string Versao)> ParesDaSemente(IEnumerable<RegraCatalogoSeedItem> itens) =>
        itens.Select(static item => (item.Codigo, item.Versao));

    private static RegraCatalogoSeedItem Item(string codigo, string versao) =>
        new(Guid.NewGuid(), codigo, versao, TipoRegra.RegraCalculo, "{}", "[]", "Base legal fictícia");
}
