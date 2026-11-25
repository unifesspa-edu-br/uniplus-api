namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Services;

public sealed class ComponentesDeItemTests
{
    [Theory]
    [InlineData(" MOEDA", " moeda")]
    [InlineData("MOEDA ", "moeda ")]
    [InlineData("MOEDA", "moeda")]
    public void De_ComponenteDeclarado_PreservaEspacos(string declarado, string esperado) =>
        ComponentesDeItem.De(declarado, false, false).Should().Be(esperado);

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void De_ComponenteAusente_UsaPadrao(string? declarado) =>
        ComponentesDeItem.De(declarado, false, false).Should().Be("padrao");
}
