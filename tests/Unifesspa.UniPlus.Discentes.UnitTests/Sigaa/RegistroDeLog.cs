namespace Unifesspa.UniPlus.Discentes.UnitTests.Sigaa;

using System.Text;

using Microsoft.Extensions.Logging;

/// <summary>
/// Captura tudo o que o código sob teste registra, para que o teste possa afirmar o que
/// não pode aparecer no log.
/// </summary>
/// <remarks>
/// Habilita todos os níveis de propósito. Um registrador que ignora os níveis mais
/// detalhados nunca veria um vazamento que só ocorre neles — e o teste passaria sem ter
/// provado nada.
/// </remarks>
internal sealed class RegistroDeLog : ILoggerProvider
{
    private readonly StringBuilder _registrado = new();
    private readonly Lock _trava = new();

    public string TudoQueFoiRegistrado
    {
        get
        {
            lock (_trava)
            {
                return _registrado.ToString();
            }
        }
    }

    public ILogger CreateLogger(string categoryName) => new Registrador(this);

    public void Dispose()
    {
    }

    private void Registrar(string linha)
    {
        lock (_trava)
        {
            _registrado.AppendLine(linha);
        }
    }

    private sealed class Registrador(RegistroDeLog destino) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);

            destino.Registrar(formatter(state, exception));

            if (exception is not null)
            {
                destino.Registrar(exception.ToString());
            }
        }
    }
}
