namespace Unifesspa.UniPlus.Kernel.UnitTests.Extensions;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Extensions;

public sealed class StringExtensionsTests
{
    [Theory(DisplayName = "ApenasDigitos remove caracteres não numéricos preservando ordem")]
    [InlineData("123.456.789-00", "12345678900")]
    [InlineData("(91) 99999-0000", "91999990000")]
    [InlineData("abc123def456", "123456")]
    [InlineData("12345", "12345")]
    public void RemoveNaoDigitos_PreservaOrdem(string entrada, string esperado)
    {
        entrada.ApenasDigitos().Should().Be(esperado);
    }

    [Theory(DisplayName = "ApenasDigitos para entrada nula, vazia ou só espaços retorna string vazia")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EntradaVaziaOuNula_RetornaVazia(string? entrada)
    {
        entrada.ApenasDigitos().Should().BeEmpty();
    }

    [Fact(DisplayName = "ApenasDigitos para entrada sem dígitos retorna string vazia")]
    public void SemDigitos_RetornaVazia()
    {
        "ABC.def-XYZ".ApenasDigitos().Should().BeEmpty();
    }

    [Fact(DisplayName = "ApenasDigitos preserva dígitos Unicode não-ASCII (ex.: árabe-índicos)")]
    public void DigitosUnicode_TambemSaoConsiderados()
    {
        // char.IsDigit considera categoria Unicode Number Decimal Digit (Nd) —
        // inclui o algarismo árabe-índico ٢ (U+0662). Test pin: assert no
        // tamanho preciso de 3 garante que uma migração para regex `[0-9]`
        // (ASCII-only) quebraria este teste, sinalizando a regressão.

        string entrada = "abc1٢3";

        entrada.ApenasDigitos().Should().HaveLength(3,
            "dígitos ASCII (1, 3) + árabe-índico (٢) — todos retidos");
    }
}
