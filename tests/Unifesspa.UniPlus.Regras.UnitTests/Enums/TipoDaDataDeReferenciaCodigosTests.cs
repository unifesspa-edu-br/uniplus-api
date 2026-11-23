namespace Unifesspa.UniPlus.Regras.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;

public sealed class TipoDaDataDeReferenciaCodigosTests
{
    [Theory]
    [InlineData(TipoDaDataDeReferencia.FimDasInscricoes, "FIM_DAS_INSCRICOES")]
    [InlineData(TipoDaDataDeReferencia.InicioDaFase, "INICIO_DA_FASE")]
    [InlineData(TipoDaDataDeReferencia.FimDaFase, "FIM_DA_FASE")]
    [InlineData(TipoDaDataDeReferencia.DataEspecifica, "DATA_ESPECIFICA")]
    [InlineData(TipoDaDataDeReferencia.DataDaApresentacao, "DATA_DA_APRESENTACAO")]
    public void Valor_Declarado_TemCodigo(TipoDaDataDeReferencia valor, string codigo)
    {
        valor.Codigo().Should().Be(codigo);
        CatalogoDe<TipoDaDataDeReferencia>.TentarObter(codigo, out TipoDaDataDeReferencia lido).Should().BeTrue();
        lido.Should().Be(valor);
    }
}
