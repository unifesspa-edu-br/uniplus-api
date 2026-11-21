namespace Unifesspa.UniPlus.Application.Abstractions.UnitTests.Respostas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public sealed class CodigoComRotuloTests
{
    public enum Fase
    {
        [CodigoDeContrato("inscricao", "Inscrição")]
        Inscricao = 1,
    }

    [Fact(DisplayName = "O valor com rótulo informado devolve o código do membro e o rótulo informado")]
    public void De_ComRotuloInformado_DevolveOCodigoDoMembroEORotuloInformado()
    {
        CodigoComRotulo<Fase> codigo = CodigoComRotulo<Fase>.De(Fase.Inscricao, "Período de inscrição");

        codigo.Codigo.Should().Be("inscricao");
        codigo.Rotulo.Should().Be("Período de inscrição");
    }

    [Theory(DisplayName = "O rótulo informado nulo, vazio ou em branco é recusado")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void De_RotuloSemTexto_Lanca(string? rotulo)
    {
        Func<CodigoComRotulo<Fase>> semRotulo = () => CodigoComRotulo<Fase>.De(Fase.Inscricao, rotulo!);

        semRotulo.Should().Throw<ArgumentException>();
    }
}
