namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

public sealed class CodigoModalidadeTests
{
    [Theory(DisplayName = "Os dez códigos do catálogo legal fixo são reconhecidos como protegidos")]
    [InlineData("AC")]
    [InlineData("AC_PCD")]
    [InlineData("LB_PPI")]
    [InlineData("LB_Q")]
    [InlineData("LB_PCD")]
    [InlineData("LB_EP")]
    [InlineData("LI_PPI")]
    [InlineData("LI_Q")]
    [InlineData("LI_PCD")]
    [InlineData("LI_EP")]
    public void EhLegalFixa_CodigoDoCatalogo_Verdadeiro(string codigo)
    {
        CodigoModalidade vo = CodigoModalidade.Criar(codigo).Value!;

        vo.EhLegalFixa.Should().BeTrue();
        CodigoModalidade.EhCodigoLegalFixo(codigo).Should().BeTrue();
    }

    [Theory(DisplayName = "Código institucional não é protegido")]
    [InlineData("PSIQ_INDIGENA")]
    [InlineData("PSVR_AMPLA")]
    [InlineData("SUP")]
    [InlineData("LB")]
    [InlineData("LB_PPI2")]
    public void EhLegalFixa_CodigoInstitucional_Falso(string codigo)
    {
        CodigoModalidade vo = CodigoModalidade.Criar(codigo).Value!;

        vo.EhLegalFixa.Should().BeFalse();
        CodigoModalidade.EhCodigoLegalFixo(codigo).Should().BeFalse();
    }

    [Theory(DisplayName = "A reserva enxerga o código com espaços à volta")]
    [InlineData(" LB_PPI ")]
    [InlineData("\tAC")]
    [InlineData("AC_PCD\n")]
    public void EhCodigoLegalFixo_ComEspacos_Verdadeiro(string codigo) =>
        CodigoModalidade.EhCodigoLegalFixo(codigo).Should().BeTrue(
            "o cadastro apara o código antes de persistir — com espaços é o mesmo código");

    [Theory(DisplayName = "Valor nulo ou em branco não é código protegido")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EhCodigoLegalFixo_NuloOuBranco_Falso(string? codigo) =>
        CodigoModalidade.EhCodigoLegalFixo(codigo).Should().BeFalse();

    [Fact(DisplayName = "O catálogo legal fixo tem exatamente dez códigos")]
    public void CodigosLegaisFixos_TemDezItens() =>
        CodigoModalidade.CodigosLegaisFixos.Should().HaveCount(10,
            "as oito modalidades da Lei 12.711/2012, a ampla concorrência e a modalidade "
            + "de pessoa com deficiência fora da reserva federal");

    [Fact(DisplayName = "A comparação do catálogo é case-sensitive")]
    public void EhCodigoLegalFixo_Minusculas_Falso() =>
        CodigoModalidade.EhCodigoLegalFixo("lb_ppi").Should().BeFalse(
            "o formato canônico do código é maiúsculo — 'lb_ppi' nem sequer é um código válido");
}
