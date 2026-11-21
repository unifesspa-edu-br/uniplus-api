namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;

public sealed class RegimesDeFuncionamentoTests
{
    [Fact(DisplayName = "INTENSIVO exige regime de turno INTEGRAL")]
    public void RegimeDeTurnoExigido_Intensivo_ExigeIntegral() =>
        RegimesDeFuncionamento.RegimeDeTurnoExigido(RegimeDeFuncionamento.Intensivo)
            .Should().Be(RegimeDeTurno.Integral);

    [Fact(DisplayName = "EXTENSIVO não restringe o regime de turno")]
    public void RegimeDeTurnoExigido_Extensivo_NaoRestringe() =>
        RegimesDeFuncionamento.RegimeDeTurnoExigido(RegimeDeFuncionamento.Extensivo)
            .Should().BeNull();

    [Fact(DisplayName = "ALTERNANCIA_PEDAGOGICA não restringe o regime de turno")]
    public void RegimeDeTurnoExigido_AlternanciaPedagogica_NaoRestringe() =>
        RegimesDeFuncionamento.RegimeDeTurnoExigido(RegimeDeFuncionamento.AlternanciaPedagogica)
            .Should().BeNull();

    [Fact(DisplayName = "RegimeDeTurnoExigido de valor que o enum não declara lança")]
    public void RegimeDeTurnoExigido_ValorNaoDeclarado_Lanca()
    {
        Action act = () => RegimesDeFuncionamento.RegimeDeTurnoExigido((RegimeDeFuncionamento)99);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
