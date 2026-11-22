namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public sealed class AbrangenciaTests
{
    [Theory(DisplayName = "Cada abrangência tem o código e o rótulo do contrato")]
    [InlineData(Abrangencia.Nacional, "NACIONAL", "Nacional")]
    [InlineData(Abrangencia.Estadual, "ESTADUAL", "Estadual")]
    [InlineData(Abrangencia.Municipal, "MUNICIPAL", "Municipal")]
    [InlineData(Abrangencia.Institucional, "INSTITUCIONAL", "Institucional")]
    public void Valor_Declarado_TemCodigoERotulo(Abrangencia abrangencia, string codigo, string rotulo)
    {
        abrangencia.Codigo().Should().Be(codigo);
        abrangencia.Rotulo().Should().Be(rotulo);
        CatalogoDe<Abrangencia>.TentarObter(codigo, out Abrangencia lida).Should().BeTrue();
        lida.Should().Be(abrangencia);
    }

    [Fact(DisplayName = "O enum tem exatamente as quatro abrangências, na ordem de exibição")]
    public void Valores_Catalogo_QuatroAbrangenciasNaOrdemDeDeclaracao() =>
        CatalogoDe<Abrangencia>.Valores().Should().Equal(
            Abrangencia.Nacional,
            Abrangencia.Estadual,
            Abrangencia.Municipal,
            Abrangencia.Institucional);

    [Theory(DisplayName = "Código fora do contrato não é abrangência, sem tolerância a espaço nem a caixa")]
    [InlineData("")]
    [InlineData(" NACIONAL")]
    [InlineData("NACIONAL ")]
    [InlineData("nacional")]
    [InlineData("Estadual")]
    [InlineData("FEDERAL")]
    [InlineData("1")]
    public void TentarObter_CodigoForaDoContrato_NaoEncontra(string codigo) =>
        CatalogoDe<Abrangencia>.TentarObter(codigo, out _).Should().BeFalse();
}
