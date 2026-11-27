namespace Unifesspa.UniPlus.Kernel.UnitTests.Domain.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

public sealed class NoFormatoDoCodigoAttributeTests
{
    [Theory(DisplayName = "O máximo do código entre 1 e o comprimento máximo do formato é aceito")]
    [InlineData(1)]
    [InlineData(60)]
    [InlineData(80)]
    public void Maximo_Valido_Guarda(int maximo) =>
        new NoFormatoDoCodigoAttribute(maximo).Maximo.Should().Be(maximo);

    [Theory(DisplayName = "O máximo do código zero, negativo ou acima do comprimento máximo do formato é recusado")]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(81)]
    public void Maximo_Invalido_Recusa(int maximo) =>
        FluentActions.Invoking(() => new NoFormatoDoCodigoAttribute(maximo))
            .Should().Throw<ArgumentOutOfRangeException>();
}
