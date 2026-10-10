namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class CodigoRecursoAcessibilidadeTests
{
    [Theory(DisplayName = "Códigos no formato fechado são aceitos e normalizados por Trim")]
    [InlineData("LEDOR")]
    [InlineData("SALA_AMAMENTACAO")]
    [InlineData("AB")]
    [InlineData("L1")]
    public void Criar_FormatoValido_Aceita(string valor)
    {
        Result<CodigoRecursoAcessibilidade> resultado = CodigoRecursoAcessibilidade.Criar($"  {valor}  ");

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Valor.Should().Be(valor);
    }

    [Theory(DisplayName = "Códigos ausentes ou em branco retornam CodigoObrigatorio")]
    [InlineData(null)]
    [InlineData("   ")]
    public void Criar_EmBranco_Falha(string? valor)
    {
        Result<CodigoRecursoAcessibilidade> resultado = CodigoRecursoAcessibilidade.Criar(valor);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(RecursoAcessibilidadeErrorCodes.CodigoObrigatorio);
    }

    [Theory(DisplayName = "Códigos fora do formato canônico retornam CodigoFormatoInvalido")]
    [InlineData("ledor")]
    [InlineData("1_LEDOR")]
    [InlineData("_LEDOR")]
    [InlineData("SALA-AMAMENTACAO")]
    [InlineData("SALA_AMAMENTAÇÃO")]
    [InlineData("L")]
    [InlineData("LEDOR_COM_UM_CODIGO_LONGO_DEMAIS_QUE_PASSA_DE_CINQUENTA")]
    public void Criar_FormatoInvalido_Falha(string valor)
    {
        Result<CodigoRecursoAcessibilidade> resultado = CodigoRecursoAcessibilidade.Criar(valor);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(RecursoAcessibilidadeErrorCodes.CodigoFormatoInvalido);
    }
}
