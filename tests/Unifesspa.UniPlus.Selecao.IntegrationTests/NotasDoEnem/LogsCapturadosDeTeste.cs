namespace Unifesspa.UniPlus.Selecao.IntegrationTests.NotasDoEnem;

using Microsoft.Extensions.Logging;

/// <summary>Substitui a fábrica de loggers do host para que todo log, de qualquer categoria, passe pela captura.</summary>
internal sealed class FabricaCapturadora(ILoggerProvider provedor) : ILoggerFactory
{
    public void AddProvider(ILoggerProvider provider)
    {
    }

    public ILogger CreateLogger(string categoryName) => provedor.CreateLogger(categoryName);

    public void Dispose()
    {
    }
}

internal sealed class LogsCapturados : ILoggerProvider
{
    private readonly System.Collections.Concurrent.ConcurrentQueue<string> _mensagens = new();

    public IReadOnlyCollection<string> Mensagens => _mensagens;

    public ILogger CreateLogger(string categoryName) => new Registrador(_mensagens);

    public void Dispose()
    {
    }

    private sealed class Registrador(System.Collections.Concurrent.ConcurrentQueue<string> destino) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            destino.Enqueue(formatter(state, exception));
            if (state is IEnumerable<KeyValuePair<string, object?>> pares)
            {
                foreach (KeyValuePair<string, object?> par in pares)
                {
                    destino.Enqueue($"{par.Key}={par.Value}");
                }
            }
        }
    }
}
