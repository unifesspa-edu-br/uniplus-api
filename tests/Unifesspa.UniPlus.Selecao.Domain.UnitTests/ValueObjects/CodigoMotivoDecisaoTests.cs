namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class CodigoMotivoDecisaoTests
{
    [Theory]
    [InlineData(" MOTIVO")]
    [InlineData("MOTIVO ")]
    [InlineData("MOTIVO\n")]
    [InlineData("A__B")]
    [InlineData("_A")]
    [InlineData("A_")]
    [InlineData("1A")]
    [InlineData("motivo_invalido")]
    public void Criar_CodigoForaDoFormato_Recusa(string valor)
    {
        DomainError erro = CodigoMotivoDecisao.Criar(valor).Errors.Should().ContainSingle().Which.Error;

        erro.Code.Should().Be(MotivoDeDecisaoErrorCodes.CodigoFormatoInvalido);
        erro.Message.Should().NotContain(valor);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Criar_CodigoAusente_RecusaComoObrigatorio(string? valor)
    {
        CodigoMotivoDecisao.Criar(valor).Errors.Should().ContainSingle()
            .Which.Error.Code.Should().Be(MotivoDeDecisaoErrorCodes.CodigoObrigatorio);
    }

    [Fact]
    public void Criar_CodigoValido_PreservaValor()
    {
        CodigoMotivoDecisao.Criar("MOTIVO_2").Value!.Valor.Should().Be("MOTIVO_2");
        CodigoMotivoDecisao.Criar("A").Value!.Valor.Should().Be("A");
    }

    [Theory]
    [InlineData(" MOTIVO")]
    [InlineData("MOTIVO\n")]
    public void Reidratar_CodigoInvalido_AcusaDefeitoSemRepetirValor(string valor)
    {
        Action reidratar = () => CodigoMotivoDecisao.Reidratar(valor);

        reidratar.Should().Throw<InvalidOperationException>().Which.Message.Should().NotContain(valor);
    }
}
