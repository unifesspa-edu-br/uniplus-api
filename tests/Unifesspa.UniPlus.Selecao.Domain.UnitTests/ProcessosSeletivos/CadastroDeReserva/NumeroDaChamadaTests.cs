namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.CadastroDeReserva;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.CadastroDeReserva;

/// <summary>O número da chamada é um inteiro a partir de 1, sem teto.</summary>
public sealed class NumeroDaChamadaTests
{
    [Theory(DisplayName = "O número a partir de 1 é aceito, inclusive o maior inteiro")]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(int.MaxValue)]
    public void Numero_APartirDeUm_Aceita(int numero)
    {
        Result<NumeroDaChamada> resultado = NumeroDaChamada.Criar(numero);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Valor.Should().Be(numero);
    }

    [Theory(DisplayName = "O número ausente, zero ou negativo é recusado sem repetir o valor informado")]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Numero_AusenteOuMenorQueUm_Recusa(int? numero)
    {
        Result<NumeroDaChamada> resultado = NumeroDaChamada.Criar(numero);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CadastroDeReservaErrorCodes.NumeroDaChamadaInvalido);
        resultado.Error.Message.Should().Be("Informe o número da chamada, um inteiro a partir de 1.");
    }

    [Fact(DisplayName = "Dois números iguais são o mesmo número da chamada")]
    public void NumerosIguais_SaoIguais()
    {
        NumeroDaChamada.Criar(3).Value.Should().Be(NumeroDaChamada.Criar(3).Value);
        NumeroDaChamada.Criar(3).Value.Should().NotBe(NumeroDaChamada.Criar(4).Value);
    }
}
