namespace Unifesspa.UniPlus.Application.Abstractions.UnitTests.Behaviors;

using FluentAssertions;

using FluentValidation;
using FluentValidation.Results;

using MediatR;

using Unifesspa.UniPlus.Application.Abstractions.Behaviors;

internal sealed record FakeRequest : IRequest<FakeResponse>;
internal sealed record FakeResponse(string Value = "ok");

internal sealed class ValidadorVazio : AbstractValidator<FakeRequest> { }

internal sealed class ValidadorComErro : AbstractValidator<FakeRequest>
{
    public ValidadorComErro()
    {
        RuleFor(r => r).Must(_ => false).WithMessage("Campo é obrigatório").WithName("Campo");
    }
}

public sealed class ValidationBehaviorTests
{
    [Fact]
    public async Task Handle_SemValidadores_DeveRetornarResultadoDoNext()
    {
        var behavior = new ValidationBehavior<FakeRequest, FakeResponse>([]);
        var expected = new FakeResponse("resultado");

        FakeResponse result = await behavior.Handle(
            new FakeRequest(),
            _ => Task.FromResult(expected),
            CancellationToken.None);

        result.Should().Be(expected);
    }

    [Fact]
    public async Task Handle_ComValidadorPassando_DeveRetornarResultadoDoNext()
    {
        var behavior = new ValidationBehavior<FakeRequest, FakeResponse>([new ValidadorVazio()]);
        var expected = new FakeResponse("sucesso");

        FakeResponse result = await behavior.Handle(
            new FakeRequest(),
            _ => Task.FromResult(expected),
            CancellationToken.None);

        result.Should().Be(expected);
    }

    [Fact]
    public async Task Handle_ComValidadorFalhando_DeveLancarValidationException()
    {
        var behavior = new ValidationBehavior<FakeRequest, FakeResponse>([new ValidadorComErro()]);

        Func<Task> act = () => behavior.Handle(
            new FakeRequest(),
            _ => Task.FromResult(new FakeResponse()),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*Campo é obrigatório*");
    }

    [Fact]
    public async Task Handle_ComMultiplosValidadoresFalhando_DeveLancarValidationExceptionComTodosErros()
    {
        var behavior = new ValidationBehavior<FakeRequest, FakeResponse>(
            [new ValidadorComErro(), new ValidadorComErro()]);

        Func<Task> act = () => behavior.Handle(
            new FakeRequest(),
            _ => Task.FromResult(new FakeResponse()),
            CancellationToken.None);

        (await act.Should().ThrowAsync<ValidationException>())
            .Which.Errors.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_NextNulo_DeveLancarArgumentNullException()
    {
        var behavior = new ValidationBehavior<FakeRequest, FakeResponse>([]);

        Func<Task> act = () => behavior.Handle(new FakeRequest(), null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
