namespace Unifesspa.UniPlus.Regras.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;

public sealed class EfeitoNaAnaliseDoDocumentoCodigosTests
{
    [Theory]
    [InlineData(EfeitoNaAnaliseDoDocumento.RecusarDocumento, "RECUSAR_DOCUMENTO")]
    [InlineData(EfeitoNaAnaliseDoDocumento.AceitarComAviso, "ACEITAR_COM_AVISO")]
    public void Valor_Declarado_TemCodigo(EfeitoNaAnaliseDoDocumento valor, string codigo)
    {
        valor.Codigo().Should().Be(codigo);
        CatalogoDe<EfeitoNaAnaliseDoDocumento>.TentarObter(codigo, out EfeitoNaAnaliseDoDocumento lido).Should().BeTrue();
        lido.Should().Be(valor);
    }
}
