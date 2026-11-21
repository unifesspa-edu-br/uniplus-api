namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;

public sealed class RegimesDeTurnoTests
{
    [Theory(DisplayName = "REGULAR exige um turno; INTEGRAL exige dois")]
    [InlineData(RegimeDeTurno.Regular, 1)]
    [InlineData(RegimeDeTurno.Integral, 2)]
    public void TurnosExigidos_PorRegime(RegimeDeTurno regime, int esperado) =>
        RegimesDeTurno.TurnosExigidos(regime).Should().Be(esperado);

    [Fact(DisplayName = "TurnosExigidos de valor que o enum não declara lança")]
    public void TurnosExigidos_ValorNaoDeclarado_Lanca()
    {
        Action act = () => RegimesDeTurno.TurnosExigidos((RegimeDeTurno)99);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
