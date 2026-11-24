namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class EtapaNosParametrosJsonTests
{
    private static readonly JsonSerializerOptions Opcoes = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static readonly Guid EtapaId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Theory]
    [InlineData("""{"$tipo":"maiorNotaEtapa","etapaRef":"44444444-4444-4444-4444-444444444444"}""")]
    [InlineData("""{"$tipo":"maiorNotaEtapa"}""")]
    [InlineData("""{"$tipo":"maiorNotaEtapa","etapaId":"44444444-4444-4444-4444-444444444444","etapaRef":"44444444-4444-4444-4444-444444444444"}""")]
    public void DesserializarDesempate_ChaveAntigaOuEtapaIdAusente_RecusaComJsonException(string json)
    {
        Action leitura = () => JsonSerializer.Deserialize<ParametrosCriterioDesempate>(json, Opcoes);

        leitura.Should().Throw<JsonException>();
    }

    [Fact]
    public void DesserializarDesempate_ComEtapaId_AceitaFormatoAtual()
    {
        const string json = """{"$tipo":"maiorNotaEtapa","etapaId":"44444444-4444-4444-4444-444444444444"}""";

        JsonSerializer.Deserialize<ParametrosCriterioDesempate>(json, Opcoes).Should().Be(new ParametrosDesempateMaiorNotaEtapa(EtapaId));
    }

    [Theory]
    [InlineData("""{"$tipo":"notaMinimaEtapa","etapaRef":"44444444-4444-4444-4444-444444444444","notaMinima":5}""")]
    [InlineData("""{"$tipo":"notaMinimaEtapa","notaMinima":5}""")]
    [InlineData("""{"$tipo":"notaMinimaEtapa","etapaId":"44444444-4444-4444-4444-444444444444","etapaRef":"44444444-4444-4444-4444-444444444444","notaMinima":5}""")]
    [InlineData("""{"$tipo":"ausenciaNaEtapa","etapaRef":"44444444-4444-4444-4444-444444444444"}""")]
    [InlineData("""{"$tipo":"ausenciaNaEtapa"}""")]
    [InlineData("""{"$tipo":"ausenciaNaEtapa","etapaId":"44444444-4444-4444-4444-444444444444","etapaRef":"44444444-4444-4444-4444-444444444444"}""")]
    public void DesserializarEliminacao_ChaveAntigaOuEtapaIdAusente_RecusaComJsonException(string json)
    {
        Action leitura = () => JsonSerializer.Deserialize<ParametrosRegraEliminacao>(json, Opcoes);

        leitura.Should().Throw<JsonException>();
    }

    [Fact]
    public void DesserializarEliminacao_ComEtapaId_AceitaFormatoAtual()
    {
        const string notaMinima = """{"$tipo":"notaMinimaEtapa","etapaId":"44444444-4444-4444-4444-444444444444","notaMinima":5}""";
        const string ausencia = """{"$tipo":"ausenciaNaEtapa","etapaId":"44444444-4444-4444-4444-444444444444"}""";

        JsonSerializer.Deserialize<ParametrosRegraEliminacao>(notaMinima, Opcoes).Should().Be(new ParametrosEliminacaoPorNotaMinimaNaEtapa(EtapaId, 5));
        JsonSerializer.Deserialize<ParametrosRegraEliminacao>(ausencia, Opcoes).Should().Be(new ParametrosEliminacaoPorAusenciaNaEtapa(EtapaId));
    }
}
