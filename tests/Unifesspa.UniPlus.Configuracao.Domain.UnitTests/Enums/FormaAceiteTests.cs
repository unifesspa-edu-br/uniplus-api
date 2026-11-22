namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public sealed class FormaAceiteTests
{
    [Theory(DisplayName = "Cada forma de aceite tem o código e o rótulo do contrato")]
    [InlineData(FormaAceite.ADefinir, "A_DEFINIR", "A definir")]
    [InlineData(FormaAceite.RegistroDigitalSemLogIp, "REGISTRO_DIGITAL_SEM_LOG_IP", "Registro digital sem log do endereço IP")]
    [InlineData(FormaAceite.RegistroDigitalComLogIp, "REGISTRO_DIGITAL_COM_LOG_IP", "Registro digital com log do endereço IP")]
    public void Valor_Declarado_TemCodigoERotulo(FormaAceite forma, string codigo, string rotulo)
    {
        forma.Codigo().Should().Be(codigo);
        forma.Rotulo().Should().Be(rotulo);
        CatalogoDe<FormaAceite>.TentarObter(codigo, out FormaAceite lida).Should().BeTrue();
        lida.Should().Be(forma);
    }

    [Fact(DisplayName = "O enum tem exatamente as três formas de aceite, na ordem de exibição")]
    public void Valores_Catalogo_TresFormasNaOrdemDeDeclaracao() =>
        CatalogoDe<FormaAceite>.Valores().Should().Equal(
            FormaAceite.ADefinir, FormaAceite.RegistroDigitalSemLogIp, FormaAceite.RegistroDigitalComLogIp);

    [Theory(DisplayName = "Código fora do contrato não é forma de aceite, sem tolerância a espaço nem a caixa")]
    [InlineData("")]
    [InlineData(" A_DEFINIR")]
    [InlineData("A_DEFINIR ")]
    [InlineData("a_definir")]
    [InlineData("ADefinir")]
    [InlineData("NENHUMA")]
    [InlineData("0")]
    public void TentarObter_CodigoForaDoContrato_NaoEncontra(string codigo) =>
        CatalogoDe<FormaAceite>.TentarObter(codigo, out _).Should().BeFalse();
}
