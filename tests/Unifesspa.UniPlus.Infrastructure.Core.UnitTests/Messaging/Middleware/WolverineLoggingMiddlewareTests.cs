namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Messaging.Middleware;

using System.Diagnostics;

using FluentAssertions;

using Microsoft.Extensions.Logging;

using Unifesspa.UniPlus.Infrastructure.Core.Messaging.Middleware;

using Wolverine;

public sealed class WolverineLoggingMiddlewareTests
{
    [Fact]
    public void Before_DeveRegistrarMessageTypeDoEnvelopeEmInformation()
    {
        FakeLogger<WolverineLoggingMiddlewareLogger> logger = new();
        Envelope envelope = new()
        {
            Message = new FakeMessage(Guid.NewGuid()),
            MessageType = "Unifesspa.Tests.FakeMessage",
        };

        Stopwatch sw = WolverineLoggingMiddleware.Before(envelope, logger);

        sw.Should().NotBeNull();
        logger.Entradas.Should().ContainSingle(e =>
            e.Level == LogLevel.Information &&
            e.Message.Contains("Unifesspa.Tests.FakeMessage", StringComparison.Ordinal));
    }

    [Fact]
    public void Before_SemMessageType_DeveCairParaNomeDoTipoCLR()
    {
        // Robustez: envelopes que ainda não passaram pelo serializer do
        // Wolverine podem ter MessageType nulo. O middleware cai para o
        // System.Type da Message para não emitir log com placeholder vazio.
        FakeLogger<WolverineLoggingMiddlewareLogger> logger = new();
        Envelope envelope = new() { Message = new FakeMessage(Guid.NewGuid()) };

        WolverineLoggingMiddleware.Before(envelope, logger);

        logger.Entradas.Should().ContainSingle(e =>
            e.Level == LogLevel.Information &&
            e.Message.Contains("FakeMessage", StringComparison.Ordinal));
    }

    [Fact]
    public void Finally_DeveRegistrarTempoEmMillisegundos()
    {
        FakeLogger<WolverineLoggingMiddlewareLogger> logger = new();
        Envelope envelope = new()
        {
            Message = new FakeMessage(Guid.NewGuid()),
            MessageType = "FakeMessage",
        };
        Stopwatch sw = Stopwatch.StartNew();

        WolverineLoggingMiddleware.Finally(envelope, sw, logger);

        logger.Entradas.Should().ContainSingle(e =>
            e.Level == LogLevel.Information &&
            e.Message.Contains("FakeMessage", StringComparison.Ordinal) &&
            e.Message.Contains("ms", StringComparison.Ordinal));
    }

    [Fact]
    public void FluxoCompleto_DeveProduzirDoisEventosNaOrdemCorreta()
    {
        FakeLogger<WolverineLoggingMiddlewareLogger> logger = new();
        Envelope envelope = new()
        {
            Message = new FakeMessage(Guid.NewGuid()),
            MessageType = "FakeMessage",
        };

        Stopwatch sw = WolverineLoggingMiddleware.Before(envelope, logger);
        WolverineLoggingMiddleware.Finally(envelope, sw, logger);

        logger.Entradas.Should().HaveCount(2);
        logger.Entradas[0].Message.Should().Contain("Processando");
        logger.Entradas[1].Message.Should().Contain("Concluído");
    }

    [Fact]
    public void Before_EnvelopeNulo_DeveLancarArgumentNullException()
    {
        FakeLogger<WolverineLoggingMiddlewareLogger> logger = new();

        Action act = () => WolverineLoggingMiddleware.Before(null!, logger);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Before_LoggerNulo_DeveLancarArgumentNullException()
    {
        Envelope envelope = new() { Message = new FakeMessage(Guid.NewGuid()) };

        Action act = () => WolverineLoggingMiddleware.Before(envelope, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Finally_StopwatchNulo_DeveLancarArgumentNullException()
    {
        FakeLogger<WolverineLoggingMiddlewareLogger> logger = new();
        Envelope envelope = new() { Message = new FakeMessage(Guid.NewGuid()) };

        Action act = () => WolverineLoggingMiddleware.Finally(envelope, null!, logger);

        act.Should().Throw<ArgumentNullException>();
    }

    internal sealed record FakeMessage(Guid Id);

    internal sealed class FakeLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entradas { get; } = [];

        IDisposable? ILogger.BeginScope<TState>(TState state) => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            this.Entradas.Add((logLevel, formatter(state, exception)));
        }
    }
}
