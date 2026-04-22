namespace Unifesspa.UniPlus.Application.Abstractions.UnitTests.Behaviors;

using FluentAssertions;

using MediatR;

using Microsoft.Extensions.Logging;

using Unifesspa.UniPlus.Application.Abstractions.Behaviors;

public sealed class LoggingBehaviorTests
{
    private sealed class FakeLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entradas { get; } = [];

        IDisposable? ILogger.BeginScope<TState>(TState state) => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Entradas.Add((logLevel, formatter(state, exception)));
        }
    }

    [Fact]
    public async Task Handle_DeveRetornarResultadoDoNext()
    {
        var logger = new FakeLogger<LoggingBehavior<FakeRequest, FakeResponse>>();
        var behavior = new LoggingBehavior<FakeRequest, FakeResponse>(logger);
        var expected = new FakeResponse("resultado");

        FakeResponse result = await behavior.Handle(
            new FakeRequest(),
            _ => Task.FromResult(expected),
            CancellationToken.None);

        result.Should().Be(expected);
    }

    [Fact]
    public async Task Handle_DeveRegistrarNomeDaRequisicaoNaEntrada()
    {
        var logger = new FakeLogger<LoggingBehavior<FakeRequest, FakeResponse>>();
        var behavior = new LoggingBehavior<FakeRequest, FakeResponse>(logger);

        await behavior.Handle(
            new FakeRequest(),
            _ => Task.FromResult(new FakeResponse()),
            CancellationToken.None);

        logger.Entradas.Should().Contain(e =>
            e.Level == LogLevel.Information &&
            e.Message.Contains("FakeRequest"));
    }

    [Fact]
    public async Task Handle_DeveRegistrarTempoDeExecucao()
    {
        var logger = new FakeLogger<LoggingBehavior<FakeRequest, FakeResponse>>();
        var behavior = new LoggingBehavior<FakeRequest, FakeResponse>(logger);

        await behavior.Handle(
            new FakeRequest(),
            _ => Task.FromResult(new FakeResponse()),
            CancellationToken.None);

        logger.Entradas.Should().Contain(e =>
            e.Level == LogLevel.Information &&
            e.Message.Contains("FakeRequest") &&
            e.Message.Contains("ms"));
    }

    [Fact]
    public async Task Handle_DeveRegistrarDoisEventosDeLog()
    {
        var logger = new FakeLogger<LoggingBehavior<FakeRequest, FakeResponse>>();
        var behavior = new LoggingBehavior<FakeRequest, FakeResponse>(logger);

        await behavior.Handle(
            new FakeRequest(),
            _ => Task.FromResult(new FakeResponse()),
            CancellationToken.None);

        logger.Entradas.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_NextNulo_DeveLancarArgumentNullException()
    {
        var logger = new FakeLogger<LoggingBehavior<FakeRequest, FakeResponse>>();
        var behavior = new LoggingBehavior<FakeRequest, FakeResponse>(logger);

        Func<Task> act = () => behavior.Handle(new FakeRequest(), null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
