namespace Unifesspa.UniPlus.Regras.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;

public sealed class EscolhaDoPrazoDeEmissaoCodigosTests
{
    [Theory]
    [InlineData(EscolhaDoPrazoDeEmissao.SemPrazo, "SEM_PRAZO")]
    [InlineData(EscolhaDoPrazoDeEmissao.ComPrazo, "COM_PRAZO")]
    public void Valor_Declarado_TemCodigo(EscolhaDoPrazoDeEmissao valor, string codigo)
    {
        valor.Codigo().Should().Be(codigo);
        CatalogoDe<EscolhaDoPrazoDeEmissao>.TentarObter(codigo, out EscolhaDoPrazoDeEmissao lido).Should().BeTrue();
        lido.Should().Be(valor);
    }
}
