namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public sealed class ProgramaDeOfertaTests
{
    [Theory(DisplayName = "Cada programa tem o código e o rótulo do contrato")]
    [InlineData(ProgramaDeOferta.Regular, "REGULAR", "Regular")]
    [InlineData(ProgramaDeOferta.FormaPara, "FORMA_PARA", "Forma Pará")]
    [InlineData(ProgramaDeOferta.Parfor, "PARFOR", "Parfor")]
    [InlineData(ProgramaDeOferta.Pronera, "PRONERA", "Pronera")]
    [InlineData(ProgramaDeOferta.Pepeti, "PEPETI", "PEPETI")]
    [InlineData(ProgramaDeOferta.OutroConvenio, "CONVENIO_OUTRO", "Outro convênio")]
    [InlineData(ProgramaDeOferta.Outro, "OUTRO", "Outro programa")]
    public void Valor_Declarado_TemCodigoERotulo(ProgramaDeOferta programa, string codigo, string rotulo)
    {
        programa.Codigo().Should().Be(codigo);
        programa.Rotulo().Should().Be(rotulo);
        CatalogoDe<ProgramaDeOferta>.TentarObter(codigo, out ProgramaDeOferta lido).Should().BeTrue();
        lido.Should().Be(programa);
    }

    [Fact(DisplayName = "O enum tem exatamente os sete programas, na ordem de exibição")]
    public void Valores_Catalogo_SeteProgramasNaOrdemDeDeclaracao() =>
        CatalogoDe<ProgramaDeOferta>.Valores().Should().Equal(
            ProgramaDeOferta.Regular,
            ProgramaDeOferta.FormaPara,
            ProgramaDeOferta.Parfor,
            ProgramaDeOferta.Pronera,
            ProgramaDeOferta.Pepeti,
            ProgramaDeOferta.OutroConvenio,
            ProgramaDeOferta.Outro);

    [Theory(DisplayName = "Código fora do contrato não é programa, sem tolerância a espaço nem a caixa")]
    [InlineData("")]
    [InlineData(" REGULAR")]
    [InlineData("regular")]
    [InlineData("Regular")]
    [InlineData("1")]
    [InlineData("PROUNI")]
    public void TentarObter_CodigoForaDoContrato_NaoEncontra(string codigo) =>
        CatalogoDe<ProgramaDeOferta>.TentarObter(codigo, out _).Should().BeFalse();
}
