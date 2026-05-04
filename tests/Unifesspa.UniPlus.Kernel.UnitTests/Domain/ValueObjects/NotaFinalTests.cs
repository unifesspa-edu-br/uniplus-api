namespace Unifesspa.UniPlus.Kernel.UnitTests.Domain.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Results;

public sealed class NotaFinalTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(5.5)]
    [InlineData(10)]
    [InlineData(100)]
    public void Criar_DadoValorValido_DeveRetornarSucesso(decimal valor)
    {
        Result<NotaFinal> resultado = NotaFinal.Criar(valor);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Valor.Should().Be(Math.Round(valor, 4));
    }

    [Fact]
    public void Criar_DadoValorNegativo_DeveRetornarFailure()
    {
        Result<NotaFinal> resultado = NotaFinal.Criar(-0.01m);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("NotaFinal.Negativa");
    }

    [Fact]
    public void Criar_DeveArredondarParaQuatroDecimais()
    {
        Result<NotaFinal> resultado = NotaFinal.Criar(7.123456789m);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Valor.Should().Be(7.1235m);
    }

    [Fact]
    public void ToString_DeveRetornarFormatoF4EmInvariantCulture()
    {
        NotaFinal nota = NotaFinal.Criar(8.5m).Value!;

        nota.ToString().Should().Be("8.5000");
    }
}
