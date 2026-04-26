namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Messaging.Middleware;

using FluentAssertions;

using FluentValidation;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Messaging.Middleware;

using Wolverine;

public sealed class WolverineValidationMiddlewareTests
{
    [Fact]
    public async Task BeforeAsync_SemValidadoresParaOTipo_DeveCompletarSemErro()
    {
        // Garantia mínima: na ausência de qualquer IValidator<TMessage>
        // registrado, o middleware é no-op e o handler segue normalmente.
        Envelope envelope = EnvelopeFor(new FakeMessage(Guid.NewGuid()));
        ServiceProvider services = new ServiceCollection().BuildServiceProvider();

        Func<Task> act = () => WolverineValidationMiddleware.BeforeAsync(
            envelope, services, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task BeforeAsync_ComValidadorPassando_DeveCompletarSemErro()
    {
        Envelope envelope = EnvelopeFor(new FakeMessage(Guid.NewGuid()));
        ServiceProvider services = BuildServices(new ValidadorVazio());

        Func<Task> act = () => WolverineValidationMiddleware.BeforeAsync(
            envelope, services, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task BeforeAsync_ComValidadorFalhando_DeveLancarValidationException()
    {
        // Protege o contrato com o pipeline: falha de validação curto-circuita
        // o handler com ValidationException, capturada pelo
        // GlobalExceptionMiddleware como ProblemDetails 400 nos controllers.
        Envelope envelope = EnvelopeFor(new FakeMessage(Guid.NewGuid()));
        ServiceProvider services = BuildServices(new ValidadorComErro());

        Func<Task> act = () => WolverineValidationMiddleware.BeforeAsync(
            envelope, services, CancellationToken.None);

        (await act.Should().ThrowAsync<ValidationException>())
            .WithMessage("*Campo é obrigatório*");
    }

    [Fact]
    public async Task BeforeAsync_ComMultiplosValidadoresFalhando_DeveAgregarTodosErros()
    {
        // Dois validators distintos, cada um com uma única RuleFor que sempre
        // falha — confirma que o middleware roda todos os validators
        // registrados em paralelo e agrega as falhas em uma única
        // ValidationException.
        Envelope envelope = EnvelopeFor(new FakeMessage(Guid.NewGuid()));
        ServiceProvider services = BuildServices(
            new ValidadorComErroNomeA(),
            new ValidadorComErroNomeB());

        Func<Task> act = () => WolverineValidationMiddleware.BeforeAsync(
            envelope, services, CancellationToken.None);

        ValidationException ex = (await act.Should().ThrowAsync<ValidationException>()).Which;
        ex.Errors.Should().Contain(e => e.PropertyName == "CampoA")
            .And.Contain(e => e.PropertyName == "CampoB");
    }

    [Fact]
    public async Task BeforeAsync_EnvelopeSemMessage_DeveSerNoOp()
    {
        // Robustez: envelopes internos do Wolverine podem chegar sem Message
        // hidratada — o middleware retorna sem buscar validators.
        Envelope envelope = new();
        ServiceProvider services = BuildServices(new ValidadorComErro());

        Func<Task> act = () => WolverineValidationMiddleware.BeforeAsync(
            envelope, services, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task BeforeAsync_EnvelopeNulo_DeveLancarArgumentNullException()
    {
        ServiceProvider services = new ServiceCollection().BuildServiceProvider();

        Func<Task> act = () => WolverineValidationMiddleware.BeforeAsync(
            null!, services, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task BeforeAsync_ServicesNulo_DeveLancarArgumentNullException()
    {
        Envelope envelope = EnvelopeFor(new FakeMessage(Guid.NewGuid()));

        Func<Task> act = () => WolverineValidationMiddleware.BeforeAsync(
            envelope, null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    private static Envelope EnvelopeFor(object message) => new() { Message = message };

    private static ServiceProvider BuildServices(params IValidator<FakeMessage>[] validators)
    {
        ServiceCollection services = new();
        foreach (IValidator<FakeMessage> validator in validators)
        {
            services.AddSingleton(validator);
        }
        return services.BuildServiceProvider();
    }

    internal sealed record FakeMessage(Guid Id);

    internal sealed class ValidadorVazio : AbstractValidator<FakeMessage>;

    internal sealed class ValidadorComErro : AbstractValidator<FakeMessage>
    {
        public ValidadorComErro()
        {
            RuleFor(m => m).Must(_ => false)
                .WithMessage("Campo é obrigatório")
                .WithName("Campo");
        }
    }

    internal sealed class ValidadorComErroNomeA : AbstractValidator<FakeMessage>
    {
        public ValidadorComErroNomeA()
        {
            RuleFor(m => m).Must(_ => false)
                .WithMessage("Falha A")
                .WithName("CampoA");
        }
    }

    internal sealed class ValidadorComErroNomeB : AbstractValidator<FakeMessage>
    {
        public ValidadorComErroNomeB()
        {
            RuleFor(m => m).Must(_ => false)
                .WithMessage("Falha B")
                .WithName("CampoB");
        }
    }
}
