namespace Unifesspa.UniPlus.Kernel.UnitTests.Domain.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class CampoDeCodigoTests
{
    private static readonly CampoDeCodigo Campo = new("Código", 80, "obrigatorio", "formato");

    [Theory]
    [InlineData("A")]
    [InlineData("ABC_123")]
    public void Recusa_CodigoValido_Aceita(string valor) => Campo.Recusa(valor, "codigo").Should().BeNull();

    [Theory]
    [InlineData("abc")]
    [InlineData(" ABC")]
    [InlineData("ABC ")]
    [InlineData("ÁBC")]
    [InlineData("ABC__DEF")]
    [InlineData("1ABC")]
    [InlineData("*")]
    public void Recusa_CodigoForaDoFormato_RecusaSemRepetirValor(string valor)
    {
        FieldError? recusa = Campo.Recusa(valor, "codigo");
        recusa.Should().NotBeNull();
        recusa!.Error.Code.Should().Be("formato");
        recusa.Error.Message.Should().NotContain(valor);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Recusa_CodigoAusente_RecusaComoObrigatorio(string? valor) =>
        Campo.Recusa(valor, "codigo")!.Error.Code.Should().Be("obrigatorio");

    [Fact]
    public void Recusa_ComprimentoAcimaDoLimite_RecusaFormato() =>
        Campo.Recusa(new string('A', 81), "codigo")!.Error.Code.Should().Be("formato");

    [Fact]
    public void Construir_MaximoAcimaDoLimite_LancaExcecao() =>
        FluentActions.Invoking(() => new CampoDeCodigo("Código", 81, "obrigatorio", "formato"))
            .Should().Throw<ArgumentOutOfRangeException>();
}
