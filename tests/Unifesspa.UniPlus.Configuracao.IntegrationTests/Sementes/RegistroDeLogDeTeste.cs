namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Sementes;

using Microsoft.Extensions.Logging;

/// <summary>O que a semente registra ao pular um item, guardado para o teste conferir.</summary>
internal sealed class RegistroDeLogDeTeste : ILogger
{
    private readonly List<string> _mensagens = [];

    public IReadOnlyList<string> Mensagens => _mensagens;

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        _mensagens.Add(formatter(state, exception));
    }
}
