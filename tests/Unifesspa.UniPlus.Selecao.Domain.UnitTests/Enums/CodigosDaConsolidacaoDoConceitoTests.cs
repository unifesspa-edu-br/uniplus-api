namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

public sealed class CodigosDaConsolidacaoDoConceitoTests
{
    [Fact]
    public void Criterio_Valores_CodigosERotulosDeclarados() =>
        Enum.GetValues<CriterioDeApuracaoDoConceito>().Select(static valor => (valor, valor.Codigo(), valor.Rotulo()))
            .Should().Equal(
                (CriterioDeApuracaoDoConceito.Unanimidade, "UNANIMIDADE", "Unanimidade"),
                (CriterioDeApuracaoDoConceito.MaioriaSimples, "MAIORIA_SIMPLES", "Maioria simples"),
                (CriterioDeApuracaoDoConceito.MaioriaAbsoluta, "MAIORIA_ABSOLUTA", "Maioria absoluta"));

    [Fact]
    public void Tratamento_Valores_CodigosERotulosDeclarados() =>
        Enum.GetValues<TratamentoDaFaltaDeApuracao>().Select(static valor => (valor, valor.Codigo(), valor.Rotulo()))
            .Should().Equal(
                (TratamentoDaFaltaDeApuracao.NovoCorretor, "NOVO_CORRETOR", "Novo corretor"),
                (TratamentoDaFaltaDeApuracao.DecisaoDaBanca, "DECISAO_DA_BANCA", "Decisão da banca"));

    [Theory(DisplayName = "O token do critério converte para o critério e volta ao mesmo token")]
    [InlineData("UNANIMIDADE", CriterioDeApuracaoDoConceito.Unanimidade)]
    [InlineData("MAIORIA_SIMPLES", CriterioDeApuracaoDoConceito.MaioriaSimples)]
    [InlineData("MAIORIA_ABSOLUTA", CriterioDeApuracaoDoConceito.MaioriaAbsoluta)]
    public void Criterio_TokenIdaEVolta(string token, CriterioDeApuracaoDoConceito esperado)
    {
        CatalogoDe<CriterioDeApuracaoDoConceito>.TentarObter(token, out CriterioDeApuracaoDoConceito criterio).Should().BeTrue();

        criterio.Should().Be(esperado);
        criterio.Codigo().Should().Be(token);
    }

    [Theory(DisplayName = "O token de critério ausente, em branco ou desconhecido é recusado")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Unanimidade")]
    [InlineData("consenso")]
    public void Criterio_TokenInvalido_Recusado(string? token) =>
        CatalogoDe<CriterioDeApuracaoDoConceito>.TentarObter(token, out _).Should().BeFalse();

    [Fact(DisplayName = "O critério fora do enum não tem token")]
    public void Criterio_ForaDoEnum_Lanca()
    {
        Func<string> projetar = () => ((CriterioDeApuracaoDoConceito)99).Codigo();

        projetar.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory(DisplayName = "O token do tratamento converte para o tratamento e volta ao mesmo token")]
    [InlineData("NOVO_CORRETOR", TratamentoDaFaltaDeApuracao.NovoCorretor)]
    [InlineData("DECISAO_DA_BANCA", TratamentoDaFaltaDeApuracao.DecisaoDaBanca)]
    public void Tratamento_TokenIdaEVolta(string token, TratamentoDaFaltaDeApuracao esperado)
    {
        CatalogoDe<TratamentoDaFaltaDeApuracao>.TentarObter(token, out TratamentoDaFaltaDeApuracao tratamento).Should().BeTrue();

        tratamento.Should().Be(esperado);
        tratamento.Codigo().Should().Be(token);
    }

    [Theory(DisplayName = "O token de tratamento ausente, em branco ou desconhecido é recusado")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("NovoCorretor")]
    [InlineData("sorteio")]
    public void Tratamento_TokenInvalido_Recusado(string? token) =>
        CatalogoDe<TratamentoDaFaltaDeApuracao>.TentarObter(token, out _).Should().BeFalse();

    [Fact(DisplayName = "O tratamento fora do enum não tem token")]
    public void Tratamento_ForaDoEnum_Lanca()
    {
        Func<string> projetar = () => ((TratamentoDaFaltaDeApuracao)99).Codigo();

        projetar.Should().Throw<ArgumentOutOfRangeException>();
    }
}
