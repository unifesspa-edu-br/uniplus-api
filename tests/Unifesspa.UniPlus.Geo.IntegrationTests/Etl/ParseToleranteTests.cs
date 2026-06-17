namespace Unifesspa.UniPlus.Geo.IntegrationTests.Etl;

using AwesomeAssertions;

using Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Etl.Parsing;

/// <summary>
/// Conversores tolerantes do ETL (ADR-0092): <c>'-'</c>/vazio/não-numérico degradam
/// para <see langword="null"/> sem lançar; decimais usam <c>InvariantCulture</c>.
/// </summary>
public sealed class ParseToleranteTests
{
    [Theory]
    [InlineData("184.413", 184.413)]
    [InlineData("0.69", 0.69)]
    [InlineData("52747856526.44", 52747856526.44)]
    [InlineData("  6.52  ", 6.52)]
    public void ParaDecimal_ValorNumerico_UsaInvariantCulture(string entrada, double esperado)
    {
        ParseTolerante.ParaDecimal(entrada).Should().Be((decimal)esperado);
    }

    [Theory]
    [InlineData("-")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("indisponível")]
    [InlineData("6,52")]          // vírgula NÃO é separador (a fonte usa ponto) → degrada, não vira 652
    [InlineData("1.234.567,89")]  // formato pt-BR é rejeitado pela mesma razão
    public void ParaDecimal_AusenteOuInvalido_DegradaParaNull(string? entrada)
    {
        ParseTolerante.ParaDecimal(entrada).Should().BeNull();
    }

    [Theory]
    [InlineData("8120131", 8120131)]
    [InlineData("1282", 1282)]
    public void ParaInteiro_ValorInteiro_Converte(string entrada, int esperado)
    {
        ParseTolerante.ParaInteiro(entrada).Should().Be(esperado);
    }

    [Theory]
    [InlineData("-")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("6.52")]
    public void ParaInteiro_AusenteOuNaoInteiro_DegradaParaNull(string? entrada)
    {
        ParseTolerante.ParaInteiro(entrada).Should().BeNull();
    }

    [Theory]
    [InlineData("S", true)]
    [InlineData("s", true)]
    [InlineData("N", false)]
    [InlineData("-", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ParaBoolSn_MapeiaSomenteS(string? entrada, bool esperado)
    {
        ParseTolerante.ParaBoolSn(entrada).Should().Be(esperado);
    }
}
