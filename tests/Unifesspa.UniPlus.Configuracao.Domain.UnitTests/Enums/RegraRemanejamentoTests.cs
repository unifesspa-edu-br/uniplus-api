namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public sealed class RegraRemanejamentoTests
{
    [Theory(DisplayName = "Cada regra de remanejamento tem o código e o rótulo do contrato")]
    [InlineData(RegraRemanejamento.SegueOrdemDeRemanejamento, "SEGUE_ORDEM_DE_REMANEJAMENTO", "Segue a ordem de remanejamento")]
    [InlineData(RegraRemanejamento.DestinoUnico, "DESTINO_UNICO", "Destino único")]
    [InlineData(RegraRemanejamento.Cruzado, "CRUZADO", "Cruzado")]
    public void Valor_Declarado_TemCodigoERotulo(RegraRemanejamento valor, string codigo, string rotulo)
    {
        valor.Codigo().Should().Be(codigo);
        valor.Rotulo().Should().Be(rotulo);
        CatalogoDe<RegraRemanejamento>.TentarObter(codigo, out RegraRemanejamento lido).Should().BeTrue();
        lido.Should().Be(valor);
    }

    [Fact(DisplayName = "O enum tem exatamente as três regras, na ordem de exibição")]
    public void Valores_Catalogo_NaOrdemDeDeclaracao() =>
        CatalogoDe<RegraRemanejamento>.Valores().Should().Equal(
            RegraRemanejamento.SegueOrdemDeRemanejamento,
            RegraRemanejamento.DestinoUnico,
            RegraRemanejamento.Cruzado);

    [Theory(DisplayName = "Código fora do contrato não é regra de remanejamento, sem tolerância a espaço nem a caixa")]
    [InlineData("")]
    [InlineData("3")]
    [InlineData("Cruzado")]
    [InlineData("cruzado")]
    [InlineData(" CRUZADO")]
    [InlineData("CRUZADO ")]
    public void TentarObter_CodigoForaDoContrato_NaoEncontra(string codigo) =>
        CatalogoDe<RegraRemanejamento>.TentarObter(codigo, out _).Should().BeFalse();
}
