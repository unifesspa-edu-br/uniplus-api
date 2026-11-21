namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public sealed class FormatoPedagogicoTests
{
    [Theory(DisplayName = "Cada formato tem o código e o rótulo do contrato")]
    [InlineData(FormatoPedagogico.Presencial, "PRESENCIAL", "Presencial")]
    [InlineData(FormatoPedagogico.Semipresencial, "SEMIPRESENCIAL", "Semipresencial")]
    [InlineData(FormatoPedagogico.Ead, "EAD", "A distância")]
    public void Valor_Declarado_TemCodigoERotulo(FormatoPedagogico formato, string codigo, string rotulo)
    {
        formato.Codigo().Should().Be(codigo);
        formato.Rotulo().Should().Be(rotulo);
        CatalogoDe<FormatoPedagogico>.TentarObter(codigo, out FormatoPedagogico lido).Should().BeTrue();
        lido.Should().Be(formato);
    }

    [Fact(DisplayName = "O enum tem exatamente os três formatos, na ordem de exibição")]
    public void Valores_Catalogo_TresFormatosNaOrdemDeDeclaracao() =>
        CatalogoDe<FormatoPedagogico>.Valores().Should().Equal(
            FormatoPedagogico.Presencial,
            FormatoPedagogico.Semipresencial,
            FormatoPedagogico.Ead);

    [Theory(DisplayName = "Código fora do contrato não é formato, sem tolerância a espaço nem a caixa")]
    [InlineData("")]
    [InlineData(" EAD")]
    [InlineData("ead")]
    [InlineData("Presencial")]
    [InlineData("2")]
    [InlineData("HIBRIDO")]
    public void TentarObter_CodigoForaDoContrato_NaoEncontra(string codigo) =>
        CatalogoDe<FormatoPedagogico>.TentarObter(codigo, out _).Should().BeFalse();
}
