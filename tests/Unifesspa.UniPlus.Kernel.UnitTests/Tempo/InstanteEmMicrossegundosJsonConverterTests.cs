namespace Unifesspa.UniPlus.Kernel.UnitTests.Tempo;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Tempo;

public sealed class InstanteEmMicrossegundosJsonConverterTests
{
    private static readonly JsonSerializerOptions Opcoes = new(JsonSerializerDefaults.Web) { Converters = { new InstanteEmMicrossegundosJsonConverter() } };

    private static readonly DateTimeOffset AberturaEmUtc = new(2026, 11, 12, 10, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "Lê o instante com 7 casas decimais descartando a 7ª, sem arredondar")]
    public void Ler_SetimaCasa_ViraSeis()
    {
        DateTimeOffset lido = JsonSerializer.Deserialize<DateTimeOffset>("\"2026-11-12T10:00:00.9999999Z\"", Opcoes);

        lido.Should().Be(AberturaEmUtc.AddTicks(9_999_990));
        (lido.Ticks % TimeSpan.TicksPerMicrosecond).Should().Be(0);
    }

    [Fact(DisplayName = "Lê o instante com offset diferente de UTC preservando o offset")]
    public void Ler_OffsetDiferenteDeUtc_PreservaOOffset()
    {
        DateTimeOffset lido = JsonSerializer.Deserialize<DateTimeOffset>("\"2026-11-12T10:00:00.1234567-03:00\"", Opcoes);

        lido.Offset.Should().Be(TimeSpan.FromHours(-3));
        lido.Ticks.Should().Be(new DateTimeOffset(2026, 11, 12, 10, 0, 0, TimeSpan.FromHours(-3)).AddTicks(1_234_560).Ticks);
    }

    [Fact(DisplayName = "Lê o instante anulável truncado e preserva a ausência")]
    public void Ler_Anulavel_TruncaOPresenteEPreservaONulo()
    {
        JsonSerializer.Deserialize<DateTimeOffset?>("\"2026-11-12T10:00:00.9999999Z\"", Opcoes)
            .Should().Be(AberturaEmUtc.AddTicks(9_999_990));
        JsonSerializer.Deserialize<DateTimeOffset?>("null", Opcoes).Should().BeNull();
    }

    [Fact(DisplayName = "Lê cada instante de uma lista truncado")]
    public void Ler_Lista_TruncaCadaItem()
    {
        List<DateTimeOffset>? lidos = JsonSerializer.Deserialize<List<DateTimeOffset>>(
            "[\"2026-11-12T10:00:00.0000001Z\",\"2026-11-12T10:00:00.0000019Z\"]", Opcoes);

        lidos.Should().Equal(AberturaEmUtc, AberturaEmUtc.AddTicks(10));
    }

    [Fact(DisplayName = "Lê o instante de objeto aninhado, anulável e não anulável, truncado")]
    public void Ler_ObjetoAninhado_TruncaAsPropriedades()
    {
        Cronograma? lido = JsonSerializer.Deserialize<Cronograma>(
            "{\"abertura\":{\"instante\":\"2026-11-12T10:00:00.9999999Z\"},\"encerramento\":\"2026-11-12T10:00:00.5000009Z\"}", Opcoes);

        lido!.Abertura.Instante.Should().Be(AberturaEmUtc.AddTicks(9_999_990));
        lido.Encerramento.Should().Be(AberturaEmUtc.AddTicks(5_000_000));
    }

    [Theory(DisplayName = "Instante que não é data e hora é recusado com JsonException")]
    [InlineData("\"amanhã de manhã\"")]
    [InlineData("\"2026-13-45T10:00:00Z\"")]
    [InlineData("20261112")]
    public void Ler_FormatoInvalido_LancaJsonException(string json)
    {
        Action ler = () => JsonSerializer.Deserialize<DateTimeOffset>(json, Opcoes);

        ler.Should().Throw<JsonException>();
    }

    [Fact(DisplayName = "Escreve o instante no formato padrão, truncado em microssegundos")]
    public void Escrever_FormatoPadraoTruncado()
    {
        DateTimeOffset instante = new DateTimeOffset(2026, 11, 12, 10, 0, 0, TimeSpan.FromHours(-3)).AddTicks(1_234_567);

        JsonSerializer.Serialize(instante, Opcoes).Should().Be("\"2026-11-12T10:00:00.123456-03:00\"");
    }

    [Fact(DisplayName = "Escreve o instante anulável e a ausência como o serializador padrão")]
    public void Escrever_Anulavel_FormatoPadrao()
    {
        JsonSerializer.Serialize<DateTimeOffset?>(AberturaEmUtc, Opcoes).Should().Be("\"2026-11-12T10:00:00+00:00\"");
        JsonSerializer.Serialize<DateTimeOffset?>(null, Opcoes).Should().Be("null");
    }

    private sealed record Cronograma(Janela Abertura, DateTimeOffset? Encerramento);

    private sealed record Janela(DateTimeOffset Instante);
}
