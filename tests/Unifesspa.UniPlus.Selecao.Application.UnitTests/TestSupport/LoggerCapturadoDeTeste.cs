namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

using Microsoft.Extensions.Logging;

/// <summary>O que um registro de log carregou: o nível, a mensagem formatada e as propriedades estruturadas.</summary>
internal sealed record RegistroDeLog(LogLevel Nivel, string Mensagem, IReadOnlyDictionary<string, object?> Propriedades);

/// <summary>O logger que guarda os registros, para o teste conferir o que foi escrito e com quais propriedades.</summary>
internal sealed class LoggerCapturadoDeTeste<T> : ILogger<T>
{
    public List<RegistroDeLog> Registros { get; } = [];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);

        Dictionary<string, object?> propriedades = state is IEnumerable<KeyValuePair<string, object?>> pares
            ? new(pares, StringComparer.Ordinal)
            : new(StringComparer.Ordinal);
        Registros.Add(new RegistroDeLog(logLevel, formatter(state, exception), propriedades));
    }
}
