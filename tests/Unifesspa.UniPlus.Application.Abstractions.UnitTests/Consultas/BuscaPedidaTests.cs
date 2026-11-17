namespace Unifesspa.UniPlus.Application.Abstractions.UnitTests.Consultas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Application.Abstractions.Consultas;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class BuscaPedidaTests
{
    private const string MensagemDeBuscaMuitoLonga = "O texto pesquisado excede 200 caracteres.";

    [Fact]
    public void Validar_TermoAusente_DevolveSucessoNulo()
    {
        Result<string?> resultado = BuscaPedida.Validar(null);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().BeNull();
    }

    [Fact]
    public void Validar_DuzentosCaracteres_Cabem()
    {
        string termo = new('a', 200);

        Result<string?> resultado = BuscaPedida.Validar(termo);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().Be(termo);
    }

    [Fact]
    public void Validar_DuzentosEUmCaracteres_RecusaComOCodigoEAMensagem()
    {
        Result<string?> resultado = BuscaPedida.Validar(new string('a', 201));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Consulta.BuscaMuitoLonga");
        resultado.Error.Message.Should().Be(MensagemDeBuscaMuitoLonga);
    }

    [Fact]
    public void Validar_DuzentosCaracteresSeguidosDeEspacos_RecusaPorqueMedeOTextoInalterado()
    {
        Result<string?> resultado = BuscaPedida.Validar(new string('a', 200) + "   ");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Consulta.BuscaMuitoLonga");
    }

    [Fact]
    public void Validar_TermoQueCabeComEspacos_DevolveOTextoInalterado()
    {
        Result<string?> resultado = BuscaPedida.Validar("  Maria da Silva  ");

        resultado.Value.Should().Be("  Maria da Silva  ");
    }
}
