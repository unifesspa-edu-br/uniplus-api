namespace Unifesspa.UniPlus.Kernel.UnitTests.Domain.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class PercentualTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(0.01)]
    [InlineData(78.50)]
    [InlineData(100)]
    public void Criar_DadoValorNoIntervalo_DeveRetornarSucesso(decimal valor)
    {
        Result<Percentual> resultado = Percentual.Criar(valor);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Valor.Should().Be(valor);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(-1)]
    [InlineData(100.01)]
    [InlineData(150)]
    public void Criar_DadoValorForaDoIntervalo_DeveRetornarFailure(decimal valor)
    {
        Result<Percentual> resultado = Percentual.Criar(valor);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Percentual.ForaDeFaixa");
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(100, true)]
    [InlineData(-0.01, false)]
    [InlineData(100.01, false)]
    public void EhValido_RefleteOIntervaloFechado(decimal valor, bool esperado)
    {
        Percentual.EhValido(valor).Should().Be(esperado);
    }

    [Fact]
    public void ToString_DeveRetornarFormatoF2EmInvariantCulture()
    {
        Percentual percentual = Percentual.Criar(8.4m).Value!;

        percentual.ToString().Should().Be("8.40");
    }
}
