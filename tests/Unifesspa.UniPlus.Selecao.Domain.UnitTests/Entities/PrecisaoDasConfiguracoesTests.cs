namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class PrecisaoDasConfiguracoesTests
{
    [Theory]
    [InlineData("0.12345", "fator")]
    [InlineData("100", "fator")]
    [InlineData("0.12345", "teto")]
    [InlineData("100", "teto")]
    public void Criar_FadaForaDaPrecisao_RecusaFormato(string texto, string campo)
    {
        decimal valor = decimal.Parse(texto, System.Globalization.CultureInfo.InvariantCulture);
        Result<ConfiguracaoDoFada> resultado = Fada(campo == "fator" ? valor : 1m, campo == "teto" ? valor : 1m);
        resultado.Errors.Should().Contain(e => e.Field == campo && e.Error.Code == CodigosDeValidacao.Formato);
    }

    [Fact]
    public void Criar_FadaNoLimite_Aceita() => Fada(99.9999m, 99.9999m).IsSuccess.Should().BeTrue();

    [Theory]
    [InlineData("50.555")]
    [InlineData("10000000000")]
    public void Criar_TaxaForaDaPrecisao_RecusaFormato(string texto)
    {
        decimal valor = decimal.Parse(texto, System.Globalization.CultureInfo.InvariantCulture);
        Result<ConfiguracaoTaxaInscricao> resultado = Taxa(valor);
        resultado.Errors.Should().Contain(e => e.Field == "valor" && e.Error.Code == CodigosDeValidacao.Formato);
    }

    [Fact]
    public void Criar_TaxaNoLimite_Aceita() => Taxa(9999999999.99m).IsSuccess.Should().BeTrue();

    [Theory]
    [InlineData("0.50001")]
    [InlineData("10")]
    public void ConferirForma_PercentualForaDaPrecisao_RecusaFormato(string texto)
    {
        decimal valor = decimal.Parse(texto, System.Globalization.CultureInfo.InvariantCulture);
        IReadOnlyList<FieldError> erros = ConferenciaDaDistribuicaoDeVagas.ConferirForma(1, valor, 1, []);
        erros.Should().Contain(e => e.Field == "percentualDaReservaLegal" && e.Error.Code == CodigosDeValidacao.Formato);
    }

    [Fact]
    public void ConferirForma_PercentualNoLimite_Aceita() =>
        ConferenciaDaDistribuicaoDeVagas.ConferirForma(1, 1.0000m, 1, []).Should().BeEmpty();

    [Fact]
    public void Criar_ValoresForaDaFaixaEDaPrecisao_AcumulaNaOrdem()
    {
        Result<ConfiguracaoDoFada> resultado = Fada(-0.00001m, -0.00001m);
        resultado.Errors.Select(e => e.Error.Code).Should().Equal(
            "ConfiguracaoDoFada.FatorInvalido", CodigosDeValidacao.Formato,
            "ConfiguracaoDoFada.TetoInvalido", CodigosDeValidacao.Formato);
    }

    [Fact]
    public void Criar_TaxaForaDaFaixaEDaPrecisao_AcumulaNaOrdem()
    {
        Result<ConfiguracaoTaxaInscricao> resultado = Taxa(-0.001m);
        resultado.Errors.Select(e => e.Error.Code).Should().Equal(
            "ConfiguracaoTaxaInscricao.ValorObrigatorioQuandoCobra", CodigosDeValidacao.Formato);
    }

    [Fact]
    public void ConferirForma_PercentualForaDaFaixaEDaPrecisao_AcumulaNaOrdem()
    {
        IReadOnlyList<FieldError> erros = ConferenciaDaDistribuicaoDeVagas.ConferirForma(1, 10.00001m, 1, []);
        erros.Select(e => e.Error.Code).Should().Equal(
            DistribuicaoDeVagasErrorCodes.PercentualDaReservaLegalForaDoLimite, CodigosDeValidacao.Formato);
    }

    private static Result<ConfiguracaoDoFada> Fada(decimal fator, decimal teto) =>
        ConfiguracaoDoFada.Criar(ReferenciaRegra.De(RegraDoFada.Multiplicativo.Codigo(), "v1", new string('a', 64)),
            fator, teto, null, new BaseLegalCongelada(Guid.CreateVersion7(), "BASE", "PORTARIA", "Portaria", "Descrição", [("1504208", "Marabá", "PA")]));

    private static Result<ConfiguracaoTaxaInscricao> Taxa(decimal valor) =>
        ConfiguracaoTaxaInscricao.Criar(true, valor, ["CADASTRO_UNICO"]);
}
