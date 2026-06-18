namespace Unifesspa.UniPlus.Geo.IntegrationTests.Cep;

using AwesomeAssertions;

using Unifesspa.UniPlus.Geo.API.Formatting;

/// <summary>
/// Decode/normalização de CEP no boundary (ADR-0031, #676): remove máscara e exige
/// exatamente 8 dígitos ASCII.
/// </summary>
public sealed class CepValidoTests
{
    [Theory(DisplayName = "Normaliza CEP válido (com ou sem máscara)")]
    [InlineData("01001000", "01001000")]
    [InlineData("01001-000", "01001000")]
    [InlineData(" 01001-000 ", "01001000")]
    [InlineData("01001 000", "01001000")]
    [InlineData("00000000", "00000000")]
    public void TentarNormalizar_Valido(string entrada, string esperado)
    {
        bool ok = CepValido.TentarNormalizar(entrada, out string? normalizado);

        ok.Should().BeTrue();
        normalizado.Should().Be(esperado);
    }

    [Theory(DisplayName = "Rejeita CEP inválido (≠8 dígitos, não-numérico, separador fora de posição, vazio)")]
    [InlineData("123")]
    [InlineData("010010000")]
    [InlineData("0100100a")]
    [InlineData("abcdefgh")]
    [InlineData("0-1-0-0-1-000")]
    [InlineData("0100 1 000")]
    [InlineData("01001--000")]
    [InlineData("0100-1000")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void TentarNormalizar_Invalido(string? entrada)
    {
        bool ok = CepValido.TentarNormalizar(entrada, out string? normalizado);

        ok.Should().BeFalse();
        normalizado.Should().BeNull();
    }
}
