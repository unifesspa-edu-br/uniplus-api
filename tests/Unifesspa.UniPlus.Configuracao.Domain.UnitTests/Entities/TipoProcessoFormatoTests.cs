namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class TipoProcessoFormatoTests
{
    [Theory]
    [InlineData("TIPO_2027")]
    [InlineData("A")]
    public void Criar_CodigoValido_PreservaValor(string codigo) =>
        TipoProcesso.Criar(codigo, "Nome", null).Value!.Codigo.Should().Be(codigo);

    [Fact]
    public void Criar_CodigoComOitentaCaracteres_Aceita() =>
        TipoProcesso.Criar(new string('A', 80), "Nome", null).IsSuccess.Should().BeTrue();

    [Theory]
    [InlineData("minusculo")]
    [InlineData(" TIPO")]
    [InlineData("TIPO ")]
    [InlineData("TÍPO")]
    [InlineData("TIPO__NOVO")]
    [InlineData("1TIPO")]
    [InlineData("*")]
    public void Criar_CodigoInvalido_RecusaFormatoSemRepetirValor(string codigo)
    {
        Result<TipoProcesso> resultado = TipoProcesso.Criar(codigo, "Nome", null);
        resultado.Error!.Code.Should().Be(TipoProcessoErrorCodes.CodigoFormatoInvalido);
        resultado.Error.Message.Should().NotContain(codigo);
    }

    [Fact]
    public void Criar_CodigoReservado_RecusaSemRepetirValor()
    {
        Result<TipoProcesso> resultado = TipoProcesso.Criar("TODOS_OS_TIPOS", "Nome", null);
        resultado.Error!.Code.Should().Be(TipoProcessoErrorCodes.CodigoReservado);
        resultado.Error.Message.Should().NotContain("TODOS_OS_TIPOS");
    }
}
