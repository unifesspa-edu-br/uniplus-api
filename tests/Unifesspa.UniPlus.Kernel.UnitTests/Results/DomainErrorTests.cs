namespace Unifesspa.UniPlus.Kernel.UnitTests.Results;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;

public sealed class DomainErrorTests
{
    [Fact(DisplayName = "DomainError expõe Code e Message recebidos no construtor")]
    public void Construtor_ExpoeCodeEMessage()
    {
        DomainError erro = new("Edital.NotFound", "Edital não encontrado.");

        erro.Code.Should().Be("Edital.NotFound");
        erro.Message.Should().Be("Edital não encontrado.");
    }

    [Fact(DisplayName = "DomainError com mesmo Code e Message é igual por valor")]
    public void Igualdade_PorValor_QuandoCodeEMessageIguais()
    {
        DomainError a = new("Cpf.Invalido", "CPF inválido.");
        DomainError b = new("Cpf.Invalido", "CPF inválido.");

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact(DisplayName = "DomainError com Code diferente é desigual")]
    public void Desigualdade_QuandoCodeDifere()
    {
        DomainError a = new("Cpf.Invalido", "msg");
        DomainError b = new("Cpf.Vazio", "msg");

        a.Should().NotBe(b);
    }

    [Fact(DisplayName = "DomainError com Message diferente é desigual")]
    public void Desigualdade_QuandoMessageDifere()
    {
        DomainError a = new("c", "uma mensagem");
        DomainError b = new("c", "outra mensagem");

        a.Should().NotBe(b);
    }
}
