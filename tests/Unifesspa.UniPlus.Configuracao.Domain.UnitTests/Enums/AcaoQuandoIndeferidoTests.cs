namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public sealed class AcaoQuandoIndeferidoTests
{
    [Theory(DisplayName = "Cada ação quando indeferido tem o código e o rótulo do contrato")]
    [InlineData(AcaoQuandoIndeferido.ReclassificarAc, "RECLASSIFICAR_AC", "Reclassificar na ampla concorrência")]
    [InlineData(AcaoQuandoIndeferido.ReclassificarRegraEdital, "RECLASSIFICAR_REGRA_EDITAL", "Reclassificar conforme a regra do edital")]
    public void Valor_Declarado_TemCodigoERotulo(AcaoQuandoIndeferido valor, string codigo, string rotulo)
    {
        valor.Codigo().Should().Be(codigo);
        valor.Rotulo().Should().Be(rotulo);
        CatalogoDe<AcaoQuandoIndeferido>.TentarObter(codigo, out AcaoQuandoIndeferido lido).Should().BeTrue();
        lido.Should().Be(valor);
    }

    [Fact(DisplayName = "O enum tem exatamente as duas ações, na ordem de exibição")]
    public void Valores_Catalogo_NaOrdemDeDeclaracao() =>
        CatalogoDe<AcaoQuandoIndeferido>.Valores().Should().Equal(
            AcaoQuandoIndeferido.ReclassificarAc,
            AcaoQuandoIndeferido.ReclassificarRegraEdital);

    [Theory(DisplayName = "Código fora do contrato não é ação quando indeferido, sem tolerância a espaço nem a caixa")]
    [InlineData("")]
    [InlineData("2")]
    [InlineData("ReclassificarAc")]
    [InlineData("reclassificar_ac")]
    [InlineData(" RECLASSIFICAR_AC")]
    [InlineData("ELIMINA")]
    public void TentarObter_CodigoForaDoContrato_NaoEncontra(string codigo) =>
        CatalogoDe<AcaoQuandoIndeferido>.TentarObter(codigo, out _).Should().BeFalse();
}
