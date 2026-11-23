namespace Unifesspa.UniPlus.Regras.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;

public sealed class UnidadeDoPrazoDeEmissaoCodigosTests
{
    [Theory]
    [InlineData(UnidadeDoPrazoDeEmissao.Dias, "DIAS")]
    [InlineData(UnidadeDoPrazoDeEmissao.Meses, "MESES")]
    [InlineData(UnidadeDoPrazoDeEmissao.Anos, "ANOS")]
    public void Valor_Declarado_TemCodigo(UnidadeDoPrazoDeEmissao valor, string codigo)
    {
        valor.Codigo().Should().Be(codigo);
        CatalogoDe<UnidadeDoPrazoDeEmissao>.TentarObter(codigo, out UnidadeDoPrazoDeEmissao lido).Should().BeTrue();
        lido.Should().Be(valor);
    }
}
