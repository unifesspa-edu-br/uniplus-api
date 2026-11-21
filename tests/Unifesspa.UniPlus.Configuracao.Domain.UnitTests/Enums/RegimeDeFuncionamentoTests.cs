namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public sealed class RegimeDeFuncionamentoTests
{
    [Theory(DisplayName = "Cada regime de funcionamento tem o código e o rótulo do contrato")]
    [InlineData(RegimeDeFuncionamento.Intensivo, "INTENSIVO", "Intensivo")]
    [InlineData(RegimeDeFuncionamento.Extensivo, "EXTENSIVO", "Extensivo")]
    [InlineData(RegimeDeFuncionamento.AlternanciaPedagogica, "ALTERNANCIA_PEDAGOGICA", "Alternância pedagógica")]
    public void Valor_Declarado_TemCodigoERotulo(RegimeDeFuncionamento regime, string codigo, string rotulo)
    {
        regime.Codigo().Should().Be(codigo);
        regime.Rotulo().Should().Be(rotulo);
        CatalogoDe<RegimeDeFuncionamento>.TentarObter(codigo, out RegimeDeFuncionamento lido).Should().BeTrue();
        lido.Should().Be(regime);
    }

    [Fact(DisplayName = "O enum tem exatamente os três regimes de funcionamento, na ordem de exibição")]
    public void Valores_Catalogo_TresRegimesNaOrdemDeDeclaracao() =>
        CatalogoDe<RegimeDeFuncionamento>.Valores().Should().Equal(
            RegimeDeFuncionamento.Intensivo,
            RegimeDeFuncionamento.Extensivo,
            RegimeDeFuncionamento.AlternanciaPedagogica);

    [Theory(DisplayName = "Código fora do contrato não é regime de funcionamento, sem tolerância a espaço nem a caixa")]
    [InlineData("")]
    [InlineData(" INTENSIVO")]
    [InlineData("INTENSIVO ")]
    [InlineData("intensivo")]
    [InlineData("AlternanciaPedagogica")]
    [InlineData("1")]
    [InlineData("SEMESTRAL")]
    public void TentarObter_CodigoForaDoContrato_NaoEncontra(string codigo) =>
        CatalogoDe<RegimeDeFuncionamento>.TentarObter(codigo, out _).Should().BeFalse();
}
