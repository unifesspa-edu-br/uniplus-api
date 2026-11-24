namespace Unifesspa.UniPlus.Regras.UnitTests.ValueObjects;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.ValueObjects;

public sealed class CondicaoDnfJsonTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Serializacao_ComCodigoDoDado_PreservaNomeEValor()
    {
        const string json = """{"codigoDoDado":"IDADE","operador":0,"valor":18}""";

        CondicaoDnf condicao = JsonSerializer.Deserialize<CondicaoDnf>(json, Options)!;
        using JsonDocument gravado = JsonDocument.Parse(JsonSerializer.Serialize(condicao, Options));

        condicao.Fato.Should().Be("IDADE");
        gravado.RootElement.GetProperty("codigoDoDado").GetString().Should().Be("IDADE");
        gravado.RootElement.TryGetProperty("fato", out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("""{"fato":"IDADE","operador":0,"valor":18}""")]
    [InlineData("""{"operador":0,"valor":18}""")]
    public void Deserialize_SemCodigoDoDado_RecusaComJsonException(string json)
    {
        Action ler = () => JsonSerializer.Deserialize<CondicaoDnf>(json, Options);

        ler.Should().Throw<JsonException>();
    }
}
