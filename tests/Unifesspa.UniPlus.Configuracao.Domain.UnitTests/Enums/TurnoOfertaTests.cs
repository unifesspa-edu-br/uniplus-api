namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public sealed class TurnoOfertaTests
{
    [Theory(DisplayName = "Cada turno tem o código e o rótulo do contrato")]
    [InlineData(TurnoOferta.Matutino, "MATUTINO", "Matutino")]
    [InlineData(TurnoOferta.Vespertino, "VESPERTINO", "Vespertino")]
    [InlineData(TurnoOferta.Noturno, "NOTURNO", "Noturno")]
    public void Valor_Declarado_TemCodigoERotulo(TurnoOferta turno, string codigo, string rotulo)
    {
        turno.Codigo().Should().Be(codigo);
        turno.Rotulo().Should().Be(rotulo);
        CatalogoDe<TurnoOferta>.TentarObter(codigo, out TurnoOferta lido).Should().BeTrue();
        lido.Should().Be(turno);
    }

    [Fact(DisplayName = "O enum tem exatamente os três turnos, na ordem do dia")]
    public void Valores_Catalogo_TresTurnosNaOrdemDeDeclaracao() =>
        CatalogoDe<TurnoOferta>.Valores().Should().Equal(
            TurnoOferta.Matutino,
            TurnoOferta.Vespertino,
            TurnoOferta.Noturno);

    [Theory(DisplayName = "Código fora do contrato não é turno, sem tolerância a espaço nem a caixa")]
    [InlineData("")]
    [InlineData(" NOTURNO")]
    [InlineData("NOTURNO ")]
    [InlineData("matutino")]
    [InlineData("Vespertino")]
    [InlineData("INTEGRAL")]
    [InlineData("1")]
    public void TentarObter_CodigoForaDoContrato_NaoEncontra(string codigo) =>
        CatalogoDe<TurnoOferta>.TentarObter(codigo, out _).Should().BeFalse();
}
