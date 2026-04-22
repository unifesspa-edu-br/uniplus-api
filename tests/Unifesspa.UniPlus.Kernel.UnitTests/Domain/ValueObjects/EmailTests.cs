namespace Unifesspa.UniPlus.Kernel.UnitTests.Domain.ValueObjects;

using FluentAssertions;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class EmailTests
{
    [Theory]
    [InlineData("usuario@exemplo.com.br", "usuario@exemplo.com.br")]
    [InlineData("USUARIO@EXEMPLO.COM", "usuario@exemplo.com")]
    [InlineData("a@b.co", "a@b.co")]
    public void Criar_DadoEmailValido_DeveRetornarSucesso(string email, string esperado)
    {
        Result<Email> resultado = Email.Criar(email);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Valor.Should().Be(esperado);
    }

    [Fact]
    public void Criar_DadoEmailNulo_DeveRetornarFailureEmailVazio()
    {
        Result<Email> resultado = Email.Criar(null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Email.Vazio");
    }

    [Fact]
    public void Criar_DadoStringVazia_DeveRetornarFailureEmailVazio()
    {
        Result<Email> resultado = Email.Criar(string.Empty);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Email.Vazio");
    }

    [Theory]
    [InlineData("semArroba")]
    [InlineData("@semlocal.com")]
    [InlineData("sem@dominio")]
    [InlineData("dois@@arroba.com")]
    public void Criar_DadoEmailInvalido_DeveRetornarFailureEmailInvalido(string emailInvalido)
    {
        Result<Email> resultado = Email.Criar(emailInvalido);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Email.Invalido");
    }

    [Fact]
    public void Criar_DadoEmailComMaiusculas_DeveNormalizarParaMinusculas()
    {
        Result<Email> resultado = Email.Criar("USUARIO@EXEMPLO.COM");

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Valor.Should().Be("usuario@exemplo.com");
    }

    [Fact]
    public void ToString_DeveRetornarValorNormalizado()
    {
        Email email = Email.Criar("usuario@exemplo.com").Value!;

        email.ToString().Should().Be("usuario@exemplo.com");
    }
}
