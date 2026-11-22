namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public sealed class NaturezaLegalTests
{
    [Theory(DisplayName = "Cada natureza legal tem o código e o rótulo do contrato")]
    [InlineData(NaturezaLegal.CotaReservada, "COTA_RESERVADA", "Cota reservada")]
    [InlineData(NaturezaLegal.Ampla, "AMPLA", "Ampla concorrência")]
    [InlineData(NaturezaLegal.AcaoAfirmativa, "ACAO_AFIRMATIVA", "Ação afirmativa")]
    public void Valor_Declarado_TemCodigoERotulo(NaturezaLegal valor, string codigo, string rotulo)
    {
        valor.Codigo().Should().Be(codigo);
        valor.Rotulo().Should().Be(rotulo);
        CatalogoDe<NaturezaLegal>.TentarObter(codigo, out NaturezaLegal lido).Should().BeTrue();
        lido.Should().Be(valor);
    }

    [Fact(DisplayName = "O enum tem exatamente as três naturezas, na ordem de exibição")]
    public void Valores_Catalogo_NaOrdemDeDeclaracao() =>
        CatalogoDe<NaturezaLegal>.Valores().Should().Equal(
            NaturezaLegal.CotaReservada,
            NaturezaLegal.Ampla,
            NaturezaLegal.AcaoAfirmativa);

    [Theory(DisplayName = "Código fora do contrato não é natureza legal, sem tolerância a espaço nem a caixa")]
    [InlineData("")]
    [InlineData("3")]
    [InlineData("AcaoAfirmativa")]
    [InlineData("acao_afirmativa")]
    [InlineData("ACAO_AFIRMATIVA ")]
    [InlineData("SUPLEMENTAR")]
    [InlineData("OUTRA_MODALIDADE")]
    public void TentarObter_CodigoForaDoContrato_NaoEncontra(string codigo) =>
        CatalogoDe<NaturezaLegal>.TentarObter(codigo, out _).Should().BeFalse();
}
