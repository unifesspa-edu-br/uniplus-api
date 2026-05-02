namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Messaging.Middleware;

using System.Diagnostics;

using AwesomeAssertions;

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
        sw.IsRunning.Should().BeTrue("Before retorna o Stopwatch já iniciado para o After/Finally consumirem");
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
    public void Concluido_DeveRegistrarSucessoComoInformationEParar()
    {
        FakeLogger<WolverineLoggingMiddlewareLogger> logger = new();
        Envelope envelope = new()
        {
            Message = new FakeMessage(Guid.NewGuid()),
            MessageType = "FakeMessage",
        };
        Stopwatch sw = Stopwatch.StartNew();

        WolverineLoggingMiddleware.Concluido(envelope, sw, logger);

        sw.IsRunning.Should().BeFalse("Concluido para o stopwatch para sinalizar conclusão bem-sucedida");
        logger.Entradas.Should().ContainSingle(e =>
            e.Level == LogLevel.Information &&
            e.Message.Contains("Concluído", StringComparison.Ordinal) &&
            e.Message.Contains("FakeMessage", StringComparison.Ordinal) &&
            e.Message.Contains("ms", StringComparison.Ordinal));
    }

    [Fact]
    public void Finally_AposConcluido_DeveSerNoOp()
    {
        // Cenário success: After (Concluido) já parou o stopwatch e logou
        // "Concluído". Finally vê stopwatch parado e não faz nada — evita
        // log duplicado e telemetria falsa de falha.
        FakeLogger<WolverineLoggingMiddlewareLogger> logger = new();
        Envelope envelope = new()
        {
            Message = new FakeMessage(Guid.NewGuid()),
            MessageType = "FakeMessage",
        };
        Stopwatch sw = Stopwatch.StartNew();
        WolverineLoggingMiddleware.Concluido(envelope, sw, logger);
        int entradasAposConcluido = logger.Entradas.Count;

        WolverineLoggingMiddleware.Finally(envelope, sw, logger);

        logger.Entradas.Should().HaveCount(entradasAposConcluido,
            "Finally roda sempre, mas em sucesso o Concluido já registrou — Finally não pode duplicar nem reportar falha falsa");
    }

    [Fact]
    public void Finally_QuandoStopwatchAindaRoda_DeveRegistrarFalhaComoWarning()
    {
        // Cenário failure: o handler lançou exceção, então After (Concluido)
        // não rodou e o stopwatch continua rodando. Finally detecta isso
        // pelo IsRunning e emite Warning com a duração até a falha — sem
        // suprimir a exceção (Wolverine continua o rethrow normalmente).
        FakeLogger<WolverineLoggingMiddlewareLogger> logger = new();
        Envelope envelope = new()
        {
            Message = new FakeMessage(Guid.NewGuid()),
            MessageType = "FakeMessage",
        };
        Stopwatch sw = Stopwatch.StartNew();

        WolverineLoggingMiddleware.Finally(envelope, sw, logger);

        sw.IsRunning.Should().BeFalse("Finally para o stopwatch após detectar falha");
        logger.Entradas.Should().ContainSingle(e =>
            e.Level == LogLevel.Warning &&
            e.Message.Contains("Falhou", StringComparison.Ordinal) &&
            e.Message.Contains("FakeMessage", StringComparison.Ordinal) &&
            e.Message.Contains("ms", StringComparison.Ordinal));
    }

    [Fact]
    public void FluxoSucessoCompleto_DeveProduzirProcessandoEConcluido()
    {
        FakeLogger<WolverineLoggingMiddlewareLogger> logger = new();
        Envelope envelope = new()
        {
            Message = new FakeMessage(Guid.NewGuid()),
            MessageType = "FakeMessage",
        };

        Stopwatch sw = WolverineLoggingMiddleware.Before(envelope, logger);
        WolverineLoggingMiddleware.Concluido(envelope, sw, logger);
        WolverineLoggingMiddleware.Finally(envelope, sw, logger);

        logger.Entradas.Should().HaveCount(2);
        logger.Entradas[0].Level.Should().Be(LogLevel.Information);
        logger.Entradas[0].Message.Should().Contain("Processando");
        logger.Entradas[1].Level.Should().Be(LogLevel.Information);
        logger.Entradas[1].Message.Should().Contain("Concluído");
    }

    [Fact]
    public void FluxoFalhaCompleto_DeveProduzirProcessandoEFalhouSemConcluido()
    {
        FakeLogger<WolverineLoggingMiddlewareLogger> logger = new();
        Envelope envelope = new()
        {
            Message = new FakeMessage(Guid.NewGuid()),
            MessageType = "FakeMessage",
        };

        Stopwatch sw = WolverineLoggingMiddleware.Before(envelope, logger);
        // Concluido não roda — simula handler que lançou exceção.
        WolverineLoggingMiddleware.Finally(envelope, sw, logger);

        logger.Entradas.Should().HaveCount(2);
        logger.Entradas[0].Level.Should().Be(LogLevel.Information);
        logger.Entradas[0].Message.Should().Contain("Processando");
        logger.Entradas[1].Level.Should().Be(LogLevel.Warning);
        logger.Entradas[1].Message.Should().Contain("Falhou");
        logger.Entradas.Should().NotContain(e => e.Message.Contains("Concluído", StringComparison.Ordinal));
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
    public void Concluido_StopwatchNulo_DeveLancarArgumentNullException()
    {
        FakeLogger<WolverineLoggingMiddlewareLogger> logger = new();
        Envelope envelope = new() { Message = new FakeMessage(Guid.NewGuid()) };

        Action act = () => WolverineLoggingMiddleware.Concluido(envelope, null!, logger);

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
