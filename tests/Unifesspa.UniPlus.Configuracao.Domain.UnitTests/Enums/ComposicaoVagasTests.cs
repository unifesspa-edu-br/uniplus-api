namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public sealed class ComposicaoVagasTests
{
    [Theory(DisplayName = "Cada composição de vagas tem o código e o rótulo do contrato")]
    [InlineData(ComposicaoVagas.RestanteDasVagasOfertadas, "RESTANTE_DAS_VAGAS_OFERTADAS", "Restante das vagas ofertadas")]
    [InlineData(ComposicaoVagas.DentroDaReservaLegal, "DENTRO_DA_RESERVA_LEGAL", "Dentro da reserva legal")]
    [InlineData(ComposicaoVagas.RetiradaDeOutraModalidade, "RETIRADA_DE_OUTRA_MODALIDADE", "Retirada de outra modalidade")]
    [InlineData(ComposicaoVagas.SuplementarAoTotal, "SUPLEMENTAR_AO_TOTAL", "Suplementar ao total")]
    public void Valor_Declarado_TemCodigoERotulo(ComposicaoVagas valor, string codigo, string rotulo)
    {
        valor.Codigo().Should().Be(codigo);
        valor.Rotulo().Should().Be(rotulo);
        CatalogoDe<ComposicaoVagas>.TentarObter(codigo, out ComposicaoVagas lido).Should().BeTrue();
        lido.Should().Be(valor);
    }

    [Fact(DisplayName = "O enum tem exatamente as quatro composições, na ordem de exibição")]
    public void Valores_Catalogo_NaOrdemDeDeclaracao() =>
        CatalogoDe<ComposicaoVagas>.Valores().Should().Equal(
            ComposicaoVagas.RestanteDasVagasOfertadas,
            ComposicaoVagas.DentroDaReservaLegal,
            ComposicaoVagas.RetiradaDeOutraModalidade,
            ComposicaoVagas.SuplementarAoTotal);

    [Theory(DisplayName = "Código fora do contrato não é composição de vagas, sem tolerância a espaço nem a caixa")]
    [InlineData("")]
    [InlineData("4")]
    [InlineData("SuplementarAoTotal")]
    [InlineData("suplementar_ao_total")]
    [InlineData("SUPLEMENTAR_AO_TOTAL ")]
    [InlineData("SUPLEMENTAR")]
    public void TentarObter_CodigoForaDoContrato_NaoEncontra(string codigo) =>
        CatalogoDe<ComposicaoVagas>.TentarObter(codigo, out _).Should().BeFalse();
}
