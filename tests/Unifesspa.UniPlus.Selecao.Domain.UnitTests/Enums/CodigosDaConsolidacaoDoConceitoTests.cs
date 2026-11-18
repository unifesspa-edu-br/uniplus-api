namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Enums;

public sealed class CodigosDaConsolidacaoDoConceitoTests
{
    [Theory(DisplayName = "O token do critério converte para o critério e volta ao mesmo token")]
    [InlineData("unanimidade", CriterioDeConsolidacaoDoConceito.Unanimidade)]
    [InlineData("maioria_simples", CriterioDeConsolidacaoDoConceito.MaioriaSimples)]
    [InlineData("maioria_absoluta", CriterioDeConsolidacaoDoConceito.MaioriaAbsoluta)]
    public void Criterio_TokenIdaEVolta(string token, CriterioDeConsolidacaoDoConceito esperado)
    {
        CriterioDeConsolidacaoDoConceitoCodigo.TentarConverter(token, out CriterioDeConsolidacaoDoConceito criterio).Should().BeTrue();

        criterio.Should().Be(esperado);
        criterio.ToCodigo().Should().Be(token);
    }

    [Theory(DisplayName = "O token de critério ausente, em branco ou desconhecido é recusado")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Unanimidade")]
    [InlineData("consenso")]
    public void Criterio_TokenInvalido_Recusado(string? token) =>
        CriterioDeConsolidacaoDoConceitoCodigo.TentarConverter(token, out _).Should().BeFalse();

    [Fact(DisplayName = "O critério fora do enum não tem token")]
    public void Criterio_ForaDoEnum_Lanca()
    {
        Func<string> projetar = () => ((CriterioDeConsolidacaoDoConceito)99).ToCodigo();

        projetar.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory(DisplayName = "O token do tratamento converte para o tratamento e volta ao mesmo token")]
    [InlineData("novo_corretor", TratamentoDaFaltaDeConsolidacao.NovoCorretor)]
    [InlineData("decisao_da_banca", TratamentoDaFaltaDeConsolidacao.DecisaoDaBanca)]
    public void Tratamento_TokenIdaEVolta(string token, TratamentoDaFaltaDeConsolidacao esperado)
    {
        TratamentoDaFaltaDeConsolidacaoCodigo.TentarConverter(token, out TratamentoDaFaltaDeConsolidacao tratamento).Should().BeTrue();

        tratamento.Should().Be(esperado);
        tratamento.ToCodigo().Should().Be(token);
    }

    [Theory(DisplayName = "O token de tratamento ausente, em branco ou desconhecido é recusado")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("NovoCorretor")]
    [InlineData("sorteio")]
    public void Tratamento_TokenInvalido_Recusado(string? token) =>
        TratamentoDaFaltaDeConsolidacaoCodigo.TentarConverter(token, out _).Should().BeFalse();

    [Fact(DisplayName = "O tratamento fora do enum não tem token")]
    public void Tratamento_ForaDoEnum_Lanca()
    {
        Func<string> projetar = () => ((TratamentoDaFaltaDeConsolidacao)99).ToCodigo();

        projetar.Should().Throw<ArgumentOutOfRangeException>();
    }
}
