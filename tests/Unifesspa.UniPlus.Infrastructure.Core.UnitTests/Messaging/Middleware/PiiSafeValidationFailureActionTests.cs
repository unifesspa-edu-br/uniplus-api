namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Messaging.Middleware;

using AwesomeAssertions;

using FluentValidation;
using FluentValidation.Results;

using Unifesspa.UniPlus.Infrastructure.Core.Messaging.Middleware;

public sealed class PiiSafeValidationFailureActionTests
{
    // Command no estilo dos reais (record) — o ToString() sintetizado expõe todos
    // os campos. É exatamente o que o FailureAction default do pacote interpola na
    // mensagem da exceção; aqui garantimos que a nossa implementação NÃO faz isso.
    private sealed record FakeCommandComPii(string Cpf, string Nome);

    [Fact]
    public void Throw_LancaValidationExceptionPreservandoAsFalhas()
    {
        PiiSafeValidationFailureAction<FakeCommandComPii> action = new();
        FakeCommandComPii message = new("123.456.789-00", "Fulano de Tal");
        List<ValidationFailure> failures =
        [
            new("Cpf", "CPF é obrigatório"),
            new("Nome", "Nome é obrigatório"),
        ];

        Action act = () => action.Throw(message, failures);

        ValidationException ex = act.Should().Throw<ValidationException>().Which;
        ex.Errors.Should().BeEquivalentTo(failures);
    }

    [Fact]
    public void Throw_NaoVazaValoresDoPayloadNaMensagem()
    {
        // Invariante LGPD (Parecer DPO 002/2026): a Exception.Message é logada pelo
        // GlobalExceptionMiddleware. Ela não pode conter os VALORES do command (CPF,
        // nome) — apenas as falhas de regra sanitizadas. Protege contra a regressão
        // do FailureAction default ("Validation failure on: {message}").
        PiiSafeValidationFailureAction<FakeCommandComPii> action = new();
        FakeCommandComPii message = new("123.456.789-00", "Fulano de Tal");
        List<ValidationFailure> failures = [new("Cpf", "CPF inválido")];

        Action act = () => action.Throw(message, failures);

        ValidationException ex = act.Should().Throw<ValidationException>().Which;
        ex.Message.Should().NotContain("123.456.789-00")
            .And.NotContain("Fulano de Tal")
            .And.Contain("CPF inválido");
    }
}
