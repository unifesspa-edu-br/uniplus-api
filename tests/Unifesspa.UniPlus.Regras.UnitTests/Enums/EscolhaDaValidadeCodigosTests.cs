namespace Unifesspa.UniPlus.Regras.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;

public sealed class EscolhaDaValidadeCodigosTests
{
    [Theory]
    [InlineData(EscolhaDaValidade.NaoExigeDocumentoValido, "NAO_EXIGE_DOCUMENTO_VALIDO")]
    [InlineData(EscolhaDaValidade.ExigeDocumentoValido, "EXIGE_DOCUMENTO_VALIDO")]
    public void Valor_Declarado_TemCodigo(EscolhaDaValidade valor, string codigo)
    {
        valor.Codigo().Should().Be(codigo);
        CatalogoDe<EscolhaDaValidade>.TentarObter(codigo, out EscolhaDaValidade lido).Should().BeTrue();
        lido.Should().Be(valor);
    }
}
