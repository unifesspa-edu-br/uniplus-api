namespace Unifesspa.UniPlus.Kernel.UnitTests.Domain.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

public sealed class FormatoDoCodigoTests
{
    [Theory]
    [InlineData("LEI_12711", true)]
    [InlineData("LB_PPI", true)]
    [InlineData("A", true)]
    [InlineData("AC_I", true)]
    [InlineData(" A", false)]
    [InlineData("A ", false)]
    [InlineData("a", false)]
    [InlineData("Á", false)]
    [InlineData("A__B", false)]
    [InlineData("_A", false)]
    [InlineData("A_", false)]
    [InlineData("1A", false)]
    [InlineData("A\n", false)]
    [InlineData("", false)]
    public void TemFormatoValido_Exemplos_DevolveResultadoEsperado(string codigo, bool esperado) =>
        FormatoDoCodigo.TemFormatoValido(codigo).Should().Be(esperado);

    [Fact]
    public void TemFormatoValido_ValorNulo_LancaExcecao() =>
        FluentActions.Invoking(() => FormatoDoCodigo.TemFormatoValido(null!)).Should().Throw<ArgumentNullException>();

    [Fact]
    public void TemFormatoValido_AcimaDoMaximoGeral_ConfereApenasOFormato()
    {
        const string codigo = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

        codigo.Length.Should().Be(81);
        FormatoDoCodigo.ComprimentoMaximo.Should().Be(80);
        FormatoDoCodigo.TemFormatoValido(codigo).Should().BeTrue();
    }
}
