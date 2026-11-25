namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class DiaNaoUtilCongeladoTests
{
    [Theory]
    [InlineData(" NACIONAL")]
    [InlineData("NACIONAL ")]
    [InlineData("nacional")]
    public void Criar_AbrangenciaDiferenteDoCodigo_Recusa(string abrangencia)
    {
        Result<DiaNaoUtilCongelado> resultado = DiaNaoUtilCongelado.Criar(new DateOnly(2026, 1, 1), abrangencia, null, null, null);

        resultado.Error!.Code.Should().Be(DiaNaoUtilCongeladoErrorCodes.AbrangenciaInvalida);
        resultado.Error.Message.Should().NotContain(abrangencia);
    }
}
