namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class CodigoCategoriaDocumentoTests
{
    [Theory(DisplayName = "Códigos no formato fechado são aceitos e normalizados por Trim")]
    [InlineData("RENDA")]
    [InlineData("DOCUMENTO_PROCESSUAL")]
    [InlineData("TITULACAO_EXPERIENCIA")]
    [InlineData("RACA_ETNIA")]
    [InlineData("LEI_12711")]
    [InlineData("AB")]
    public void Criar_FormatoValido_Aceita(string valor)
    {
        Result<CodigoCategoriaDocumento> resultado = CodigoCategoriaDocumento.Criar($"  {valor}  ");

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Valor.Should().Be(valor);
    }

    [Theory(DisplayName = "Códigos ausentes ou em branco retornam CodigoObrigatorio")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_EmBranco_Falha(string? valor)
    {
        Result<CodigoCategoriaDocumento> resultado = CodigoCategoriaDocumento.Criar(valor);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CategoriaDocumentoErrorCodes.CodigoObrigatorio);
    }

    [Theory(DisplayName = "Códigos fora do formato retornam CodigoFormatoInvalido")]
    [InlineData("renda")]
    [InlineData("01")]
    [InlineData("1RENDA")]
    [InlineData("_RENDA")]
    [InlineData("R")]
    [InlineData("RACA-ETNIA")]
    [InlineData("RAÇA_ETNIA")]
    public void Criar_FormatoInvalido_Falha(string valor)
    {
        Result<CodigoCategoriaDocumento> resultado = CodigoCategoriaDocumento.Criar(valor);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CategoriaDocumentoErrorCodes.CodigoFormatoInvalido);
    }

    [Fact(DisplayName = "Código no limite de 50 caracteres é aceito e o de 51 é recusado")]
    public void Criar_NoLimiteDeTamanho_DiscriminaAceitoDeRecusado()
    {
        string cinquenta = "A" + new string('X', 49);
        string cinquentaEUm = "A" + new string('X', 50);

        CodigoCategoriaDocumento.Criar(cinquenta).IsSuccess.Should().BeTrue();
        CodigoCategoriaDocumento.Criar(cinquentaEUm).IsFailure.Should().BeTrue();
    }

    [Theory(DisplayName = "EhValido reflete o formato fechado sem alocar value object")]
    [InlineData("RENDA", true)]
    [InlineData("renda", false)]
    [InlineData("", false)]
    [InlineData("R", false)]
    public void EhValido_RefleteFormato(string valor, bool esperado)
    {
        CodigoCategoriaDocumento.EhValido(valor).Should().Be(esperado);
    }
}
