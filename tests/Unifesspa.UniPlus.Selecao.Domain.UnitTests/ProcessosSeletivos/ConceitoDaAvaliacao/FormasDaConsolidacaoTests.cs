namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.ConceitoDaAvaliacao;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Cada versão de regra de consolidação declara as formas de conceito a que se aplica, como dado da
/// versão (ADR-0150): a regra incompatível com o conceito da etapa é recusada, e a etapa com regra
/// de consolidação e sem conceito é pendência.
/// </summary>
public sealed class FormasDaConsolidacaoTests
{
    private const string HashQualquer = "0000000000000000000000000000000000000000000000000000000000000000";

    private static RegraCatalogo Regra(string codigo, string versao, string declaracaoDaRegra)
    {
        using JsonDocument esquema = JsonDocument.Parse(declaracaoDaRegra);
        using JsonDocument invariantes = JsonDocument.Parse("[]");
        return RegraCatalogo.Criar(codigo, versao, TipoRegra.RegraApuracaoAvaliacao, esquema.RootElement, invariantes.RootElement, "Edital do processo seletivo");
    }

    private static RegraCatalogo ComFormas(string codigo, string versao, params string[] formas) =>
        Regra(codigo, versao, JsonSerializer.Serialize(new Dictionary<string, string[]> { ["formas_do_conceito"] = formas }));

    private static ConceitoDaEtapa Numerico() =>
        ConceitoDaEtapa.Criar("NOTA", "Nota", FormaDoConceito.Numerico, new EscalaInformada(0m, 10m, 2), null).Value!;

    private static ConceitoDaEtapa Categorico() =>
        ConceitoDaEtapa.Criar(
            "APTO_INAPTO", "Apto ou inapto", FormaDoConceito.PorOpcoes, null,
            [new ValorCategoricoInformado("APTO", "Apto", true), new ValorCategoricoInformado("INAPTO", "Inapto", false)]).Value!;

    private static ReferenciaRegra ReferenciaDaRegra() =>
        ReferenciaRegra.De(RegraDeApuracaoDaAvaliacao.AvaliacaoUnica.Codigo(), "v1", HashQualquer);

    private static IReadOnlyList<FormaDoConceito> AvaliacaoUnica() =>
        FormasDaConsolidacao.De(ComFormas(RegraDeApuracaoDaAvaliacao.AvaliacaoUnica.Codigo(), "v1", "NUMERICO", "POR_OPCOES"));

    private static IReadOnlyList<FormaDoConceito> CorrecaoIndependente() =>
        FormasDaConsolidacao.De(ComFormas(RegraDeApuracaoDaAvaliacao.CorrecaoIndependente.Codigo(), "v1", "NUMERICO"));

    [Fact(DisplayName = "As formas saem do esquema da versão, na ordem declarada")]
    public void De_LeAsNaturezasDaVersao()
    {
        AvaliacaoUnica().Should().Equal(FormaDoConceito.Numerico, FormaDoConceito.PorOpcoes);
        CorrecaoIndependente().Should().Equal(FormaDoConceito.Numerico);
    }

    [Fact(DisplayName = "A natureza repetida no esquema conta uma vez")]
    public void De_NaturezaRepetida_ContaUmaVez()
    {
        FormasDaConsolidacao.De(ComFormas("CONSOLIDACAO-CATEGORICA", "v1", "POR_OPCOES", "POR_OPCOES"))
            .Should().Equal(FormaDoConceito.PorOpcoes);
    }

    [Fact(DisplayName = "O esquema sem a chave das formas é defeito de semente")]
    public void De_SemAChave_Lanca()
    {
        RegraCatalogo regra = Regra("APURACAO_CORRECAO_INDEPENDENTE", "v1", """{"corretores_distintos":3}""");

        Action ler = () => FormasDaConsolidacao.De(regra);

        ler.Should().Throw<ArgumentException>();
    }

    [Theory(DisplayName = "O esquema com a chave vazia, fora de uma lista ou com token desconhecido é defeito de semente")]
    [InlineData("""{"formas_do_conceito":[]}""")]
    [InlineData("""{"formas_do_conceito":"NUMERICO"}""")]
    [InlineData("""{"formas_do_conceito":["NUMERICO","por_opcoes"]}""")]
    [InlineData("""{"formas_do_conceito":["numerico"]}""")]
    [InlineData("""{"formas_do_conceito":[""]}""")]
    [InlineData("""{"formas_do_conceito":[null]}""")]
    [InlineData("""{"formas_do_conceito":[1]}""")]
    public void De_EsquemaInvalido_Lanca(string esquema)
    {
        RegraCatalogo regra = Regra("APURACAO_CORRECAO_INDEPENDENTE", "v1", esquema);

        Action ler = () => FormasDaConsolidacao.De(regra);

        ler.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "A consolidação por correção independente com conceito categórico é recusada")]
    public void Recusa_CorrecaoIndependenteComCategorico_Recusa()
    {
        FieldError? recusa = FormasDaConsolidacao.Recusa(Categorico(), CorrecaoIndependente());

        recusa.Should().NotBeNull();
        recusa!.Field.Should().Be("regraConsolidacao");
        recusa.Error.Code.Should().Be(ConceitoDaEtapaErrorCodes.ConsolidacaoIncompativel);
    }

    [Fact(DisplayName = "A consolidação por avaliação única aceita o conceito numérico e o categórico")]
    public void Recusa_AvaliacaoUnica_AceitaAsDuasNaturezas()
    {
        FormasDaConsolidacao.Recusa(Numerico(), AvaliacaoUnica()).Should().BeNull();
        FormasDaConsolidacao.Recusa(Categorico(), AvaliacaoUnica()).Should().BeNull();
    }

    [Fact(DisplayName = "A consolidação por correção independente aceita o conceito numérico")]
    public void Recusa_CorrecaoIndependenteComNumerico_Aceita()
    {
        FormasDaConsolidacao.Recusa(Numerico(), CorrecaoIndependente()).Should().BeNull();
    }

    [Fact(DisplayName = "A regra de consolidação categórica com conceito numérico é recusada")]
    public void Recusa_RegraCategoricaComNumerico_Recusa()
    {
        IReadOnlyList<FormaDoConceito> categorica = FormasDaConsolidacao.De(ComFormas("CONSOLIDACAO-CATEGORICA", "v1", "POR_OPCOES"));

        FormasDaConsolidacao.Recusa(Numerico(), categorica)!.Error.Code.Should().Be(ConceitoDaEtapaErrorCodes.ConsolidacaoIncompativel);
        FormasDaConsolidacao.Recusa(Categorico(), categorica).Should().BeNull();
    }

    [Fact(DisplayName = "A versão nova da regra com mais formas não muda o que a versão antiga aceita")]
    public void Recusa_VersaoNovaNaoAlteraAVersaoAntiga()
    {
        IReadOnlyList<FormaDoConceito> antiga = FormasDaConsolidacao.De(ComFormas("CONSOLIDACAO-X", "v1", "NUMERICO"));
        IReadOnlyList<FormaDoConceito> nova = FormasDaConsolidacao.De(ComFormas("CONSOLIDACAO-X", "v2", "NUMERICO", "POR_OPCOES"));

        FormasDaConsolidacao.Recusa(Categorico(), antiga).Should().NotBeNull();
        FormasDaConsolidacao.Recusa(Categorico(), nova).Should().BeNull();
    }

    [Fact(DisplayName = "A etapa com regra de consolidação e sem conceito é pendência no campo do conceito")]
    public void PendenciaSemConceito_ComRegraESemConceito_ApontaOConceito()
    {
        FieldError? pendencia = FormasDaConsolidacao.PendenciaSemConceito(ReferenciaDaRegra(), conceito: null);

        pendencia.Should().NotBeNull();
        pendencia!.Field.Should().Be("conceito");
        pendencia.Error.Code.Should().Be(ConceitoDaEtapaErrorCodes.ConsolidacaoSemConceito);
    }

    [Fact(DisplayName = "A etapa sem regra de consolidação não tem pendência, com ou sem conceito")]
    public void PendenciaSemConceito_SemRegra_NaoTemPendencia()
    {
        FormasDaConsolidacao.PendenciaSemConceito(regraConsolidacao: null, conceito: null).Should().BeNull();
        FormasDaConsolidacao.PendenciaSemConceito(regraConsolidacao: null, Numerico()).Should().BeNull();
    }

    [Fact(DisplayName = "A etapa com regra de consolidação e com conceito não tem pendência")]
    public void PendenciaSemConceito_ComRegraEComConceito_NaoTemPendencia()
    {
        FormasDaConsolidacao.PendenciaSemConceito(ReferenciaDaRegra(), Categorico()).Should().BeNull();
    }
}
