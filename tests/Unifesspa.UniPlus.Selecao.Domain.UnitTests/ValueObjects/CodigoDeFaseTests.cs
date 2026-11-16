namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Xunit;

/// <summary>O código de fase normalizado: o aparado, ou nulo quando está em branco.</summary>
public sealed class CodigoDeFaseTests
{
    [Theory(DisplayName = "Ausência e texto em branco são o mesmo estado: nulo")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalizado_AusenteOuEmBranco_ENulo(string? codigo)
    {
        CodigoDeFase.Normalizado(codigo).Should().BeNull();
    }

    [Theory(DisplayName = "O código é aparado")]
    [InlineData("AVALIACAO", "AVALIACAO")]
    [InlineData("  AVALIACAO ", "AVALIACAO")]
    public void Normalizado_ComEspacos_EAparado(string codigo, string esperado)
    {
        CodigoDeFase.Normalizado(codigo).Should().Be(esperado);
    }
}
