namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class CodigoCondicaoTests
{
    [Theory(DisplayName = "Códigos no formato fechado são aceitos e normalizados por Trim")]
    [InlineData("PCD")]
    [InlineData("DISLEXIA")]
    [InlineData("LACTANTE")]
    [InlineData("TEA_NIVEL_2")]
    [InlineData("AB")]
    public void Criar_FormatoValido_Aceita(string valor)
    {
        Result<CodigoCondicao> resultado = CodigoCondicao.Criar($"  {valor}  ");

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Valor.Should().Be(valor);
    }

    [Theory(DisplayName = "Códigos ausentes ou em branco retornam CodigoObrigatorio")]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_EmBranco_Falha(string valor)
    {
        Result<CodigoCondicao> resultado = CodigoCondicao.Criar(valor);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CondicaoAtendimentoErrorCodes.CodigoObrigatorio);
    }

    [Theory(DisplayName = "Códigos fora do formato retornam CodigoFormatoInvalido")]
    [InlineData("pcd")]
    [InlineData("1PCD")]
    [InlineData("_PCD")]
    [InlineData("P")]
    [InlineData("PCD-A")]
    [InlineData("PÇD")]
    public void Criar_FormatoInvalido_Falha(string valor)
    {
        Result<CodigoCondicao> resultado = CodigoCondicao.Criar(valor);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CondicaoAtendimentoErrorCodes.CodigoFormatoInvalido);
    }

    [Fact(DisplayName = "EhProtegido é verdadeiro apenas para o código reservado PCD")]
    public void EhProtegido_SomenteParaPcd()
    {
        CodigoCondicao.Criar(CodigoCondicao.Pcd).Value!.EhProtegido.Should().BeTrue();
        CodigoCondicao.Criar("DISLEXIA").Value!.EhProtegido.Should().BeFalse();
    }

    [Theory(DisplayName = "EhValido reflete o formato fechado sem alocar value object")]
    [InlineData("PCD", true)]
    [InlineData("pcd", false)]
    [InlineData("", false)]
    [InlineData("A", false)]
    public void EhValido_RefleteFormato(string valor, bool esperado)
    {
        CodigoCondicao.EhValido(valor).Should().Be(esperado);
    }
}
