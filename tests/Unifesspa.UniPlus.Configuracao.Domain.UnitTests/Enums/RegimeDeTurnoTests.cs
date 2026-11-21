namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public sealed class RegimeDeTurnoTests
{
    [Theory(DisplayName = "Cada regime de turno tem o código e o rótulo do contrato")]
    [InlineData(RegimeDeTurno.Regular, "REGULAR", "Regular")]
    [InlineData(RegimeDeTurno.Integral, "INTEGRAL", "Integral")]
    public void Valor_Declarado_TemCodigoERotulo(RegimeDeTurno regime, string codigo, string rotulo)
    {
        regime.Codigo().Should().Be(codigo);
        regime.Rotulo().Should().Be(rotulo);
        CatalogoDe<RegimeDeTurno>.TentarObter(codigo, out RegimeDeTurno lido).Should().BeTrue();
        lido.Should().Be(regime);
    }

    [Fact(DisplayName = "O enum tem exatamente os dois regimes de turno, na ordem de exibição")]
    public void Valores_Catalogo_DoisRegimesNaOrdemDeDeclaracao() =>
        CatalogoDe<RegimeDeTurno>.Valores().Should().Equal(RegimeDeTurno.Regular, RegimeDeTurno.Integral);

    [Theory(DisplayName = "Código fora do contrato não é regime de turno, sem tolerância a espaço nem a caixa")]
    [InlineData("")]
    [InlineData(" INTEGRAL")]
    [InlineData("INTEGRAL ")]
    [InlineData("integral")]
    [InlineData("Regular")]
    [InlineData("2")]
    [InlineData("PARCIAL")]
    public void TentarObter_CodigoForaDoContrato_NaoEncontra(string codigo) =>
        CatalogoDe<RegimeDeTurno>.TentarObter(codigo, out _).Should().BeFalse();
}
