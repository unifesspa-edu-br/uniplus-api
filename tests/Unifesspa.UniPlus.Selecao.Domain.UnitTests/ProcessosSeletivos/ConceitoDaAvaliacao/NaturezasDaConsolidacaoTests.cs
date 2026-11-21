namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.ConceitoDaAvaliacao;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Cada versão de regra de consolidação declara as naturezas de conceito a que se aplica, como dado da
/// versão (ADR-0150): a regra incompatível com o conceito da etapa é recusada, e a etapa com regra
/// de consolidação e sem conceito é pendência.
/// </summary>
public sealed class NaturezasDaConsolidacaoTests
{
    private const string HashQualquer = "0000000000000000000000000000000000000000000000000000000000000000";

    private static RegraCatalogo Regra(string codigo, string versao, string declaracaoDaRegra)
    {
        using JsonDocument esquema = JsonDocument.Parse(declaracaoDaRegra);
        using JsonDocument invariantes = JsonDocument.Parse("[]");
        return RegraCatalogo.Criar(codigo, versao, TipoRegra.RegraConsolidacao, esquema.RootElement, invariantes.RootElement, "Edital do processo seletivo");
    }

    private static RegraCatalogo ComNaturezas(string codigo, string versao, params string[] naturezas) =>
        Regra(codigo, versao, JsonSerializer.Serialize(new Dictionary<string, string[]> { ["naturezas_do_conceito"] = naturezas }));

    private static ConceitoDaEtapa Numerico() =>
        ConceitoDaEtapa.Criar("NOTA", "Nota", NaturezaConceito.Numerico, new EscalaInformada(0m, 10m, 2), null).Value!;

    private static ConceitoDaEtapa Categorico() =>
        ConceitoDaEtapa.Criar(
            "APTO_INAPTO", "Apto ou inapto", NaturezaConceito.Categorico, null,
            [new ValorCategoricoInformado("APTO", "Apto", true), new ValorCategoricoInformado("INAPTO", "Inapto", false)]).Value!;

    private static ReferenciaRegra ReferenciaDaRegra() =>
        ReferenciaRegra.De(RegraConsolidacaoCodigo.AvaliacaoUnica, "v1", HashQualquer);

    private static IReadOnlyList<NaturezaConceito> AvaliacaoUnica() =>
        NaturezasDaConsolidacao.De(ComNaturezas(RegraConsolidacaoCodigo.AvaliacaoUnica, "v1", "NUMERICO", "CATEGORICO"));

    private static IReadOnlyList<NaturezaConceito> CorrecaoIndependente() =>
        NaturezasDaConsolidacao.De(ComNaturezas(RegraConsolidacaoCodigo.CorrecaoIndependente, "v1", "NUMERICO"));

    [Fact(DisplayName = "As naturezas saem do esquema da versão, na ordem declarada")]
    public void De_LeAsNaturezasDaVersao()
    {
        AvaliacaoUnica().Should().Equal(NaturezaConceito.Numerico, NaturezaConceito.Categorico);
        CorrecaoIndependente().Should().Equal(NaturezaConceito.Numerico);
    }

    [Fact(DisplayName = "A natureza repetida no esquema conta uma vez")]
    public void De_NaturezaRepetida_ContaUmaVez()
    {
        NaturezasDaConsolidacao.De(ComNaturezas("CONSOLIDACAO-CATEGORICA", "v1", "CATEGORICO", "CATEGORICO"))
            .Should().Equal(NaturezaConceito.Categorico);
    }

    [Fact(DisplayName = "O esquema sem a chave das naturezas é defeito de semente")]
    public void De_SemAChave_Lanca()
    {
        RegraCatalogo regra = Regra("CONSOLIDACAO-CORRECAO-INDEPENDENTE", "v1", """{"corretores_distintos":3}""");

        Action ler = () => NaturezasDaConsolidacao.De(regra);

        ler.Should().Throw<ArgumentException>();
    }

    [Theory(DisplayName = "O esquema com a chave vazia, fora de uma lista ou com token desconhecido é defeito de semente")]
    [InlineData("""{"naturezas_do_conceito":[]}""")]
    [InlineData("""{"naturezas_do_conceito":"NUMERICO"}""")]
    [InlineData("""{"naturezas_do_conceito":["NUMERICO","Categorico"]}""")]
    [InlineData("""{"naturezas_do_conceito":["numerico"]}""")]
    [InlineData("""{"naturezas_do_conceito":[""]}""")]
    [InlineData("""{"naturezas_do_conceito":[null]}""")]
    [InlineData("""{"naturezas_do_conceito":[1]}""")]
    public void De_EsquemaInvalido_Lanca(string esquema)
    {
        RegraCatalogo regra = Regra("CONSOLIDACAO-CORRECAO-INDEPENDENTE", "v1", esquema);

        Action ler = () => NaturezasDaConsolidacao.De(regra);

        ler.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "A consolidação por correção independente com conceito categórico é recusada")]
    public void Recusa_CorrecaoIndependenteComCategorico_Recusa()
    {
        FieldError? recusa = NaturezasDaConsolidacao.Recusa(Categorico(), CorrecaoIndependente());

        recusa.Should().NotBeNull();
        recusa!.Field.Should().Be("regraConsolidacao");
        recusa.Error.Code.Should().Be(ConceitoDaEtapaErrorCodes.ConsolidacaoIncompativel);
    }

    [Fact(DisplayName = "A consolidação por avaliação única aceita o conceito numérico e o categórico")]
    public void Recusa_AvaliacaoUnica_AceitaAsDuasNaturezas()
    {
        NaturezasDaConsolidacao.Recusa(Numerico(), AvaliacaoUnica()).Should().BeNull();
        NaturezasDaConsolidacao.Recusa(Categorico(), AvaliacaoUnica()).Should().BeNull();
    }

    [Fact(DisplayName = "A consolidação por correção independente aceita o conceito numérico")]
    public void Recusa_CorrecaoIndependenteComNumerico_Aceita()
    {
        NaturezasDaConsolidacao.Recusa(Numerico(), CorrecaoIndependente()).Should().BeNull();
    }

    [Fact(DisplayName = "A regra de consolidação categórica com conceito numérico é recusada")]
    public void Recusa_RegraCategoricaComNumerico_Recusa()
    {
        IReadOnlyList<NaturezaConceito> categorica = NaturezasDaConsolidacao.De(ComNaturezas("CONSOLIDACAO-CATEGORICA", "v1", "CATEGORICO"));

        NaturezasDaConsolidacao.Recusa(Numerico(), categorica)!.Error.Code.Should().Be(ConceitoDaEtapaErrorCodes.ConsolidacaoIncompativel);
        NaturezasDaConsolidacao.Recusa(Categorico(), categorica).Should().BeNull();
    }

    [Fact(DisplayName = "A versão nova da regra com mais naturezas não muda o que a versão antiga aceita")]
    public void Recusa_VersaoNovaNaoAlteraAVersaoAntiga()
    {
        IReadOnlyList<NaturezaConceito> antiga = NaturezasDaConsolidacao.De(ComNaturezas("CONSOLIDACAO-X", "v1", "NUMERICO"));
        IReadOnlyList<NaturezaConceito> nova = NaturezasDaConsolidacao.De(ComNaturezas("CONSOLIDACAO-X", "v2", "NUMERICO", "CATEGORICO"));

        NaturezasDaConsolidacao.Recusa(Categorico(), antiga).Should().NotBeNull();
        NaturezasDaConsolidacao.Recusa(Categorico(), nova).Should().BeNull();
    }

    [Fact(DisplayName = "A etapa com regra de consolidação e sem conceito é pendência no campo do conceito")]
    public void PendenciaSemConceito_ComRegraESemConceito_ApontaOConceito()
    {
        FieldError? pendencia = NaturezasDaConsolidacao.PendenciaSemConceito(ReferenciaDaRegra(), conceito: null);

        pendencia.Should().NotBeNull();
        pendencia!.Field.Should().Be("conceito");
        pendencia.Error.Code.Should().Be(ConceitoDaEtapaErrorCodes.ConsolidacaoSemConceito);
    }

    [Fact(DisplayName = "A etapa sem regra de consolidação não tem pendência, com ou sem conceito")]
    public void PendenciaSemConceito_SemRegra_NaoTemPendencia()
    {
        NaturezasDaConsolidacao.PendenciaSemConceito(regraConsolidacao: null, conceito: null).Should().BeNull();
        NaturezasDaConsolidacao.PendenciaSemConceito(regraConsolidacao: null, Numerico()).Should().BeNull();
    }

    [Fact(DisplayName = "A etapa com regra de consolidação e com conceito não tem pendência")]
    public void PendenciaSemConceito_ComRegraEComConceito_NaoTemPendencia()
    {
        NaturezasDaConsolidacao.PendenciaSemConceito(ReferenciaDaRegra(), Categorico()).Should().BeNull();
    }
}
