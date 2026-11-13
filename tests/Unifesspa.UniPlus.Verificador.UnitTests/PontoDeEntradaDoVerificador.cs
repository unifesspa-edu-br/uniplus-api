namespace Unifesspa.UniPlus.Verificador.UnitTests;

using System.Diagnostics;
using System.Reflection;

using Microsoft.Extensions.Hosting;

using Unifesspa.UniPlus.Verificador.Configuracao;

/// <summary>
/// Executa o ponto de entrada real do verificador até o host ser construído e o devolve sem iniciá-lo: a composição
/// completa, com tudo que o <c>Program</c> registra, e sem conectar a RabbitMQ, MinIO ou clamd.
/// </summary>
internal static class PontoDeEntradaDoVerificador
{
    public static IHost ConstruirSemIniciar(params string[] argumentos)
    {
        using CapturaDoHostConstruido captura = new();
        Thread execucao = new(() => captura.Executar(() => ExecutarPontoDeEntrada(argumentos)));
        execucao.Start();
        execucao.Join();

        return captura.Host ?? throw new InvalidOperationException(
            "O ponto de entrada terminou sem construir o host.", captura.Falha);
    }

    private static void ExecutarPontoDeEntrada(string[] argumentos)
    {
        MethodInfo entrada = typeof(VerificadorOptions).Assembly.EntryPoint!;
        ((Task)entrada.Invoke(null, [argumentos])!).GetAwaiter().GetResult();
    }

    /// <summary>
    /// O evento de host construído é síncrono e acontece na thread que chama <c>Build</c>: só o da thread de execução
    /// é capturado, e a exceção que ele lança impede o <c>RunAsync</c> de iniciar o host.
    /// </summary>
    private sealed class CapturaDoHostConstruido : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>, IDisposable
    {
        private const string ListenerDoHost = "Microsoft.Extensions.Hosting";
        private const string EventoHostConstruido = "HostBuilt";

        private readonly IDisposable _assinaturaDosListeners;
        private readonly List<IDisposable> _assinaturasDosEventos = [];
        private int _threadDaExecucao;

        public CapturaDoHostConstruido() => _assinaturaDosListeners = DiagnosticListener.AllListeners.Subscribe(this);

        public IHost? Host { get; private set; }

        public Exception? Falha { get; private set; }

        public void Executar(Action ponto)
        {
            _threadDaExecucao = Environment.CurrentManagedThreadId;
            Exception? falha = Record.Exception(ponto);

            // A captura interrompe o ponto de entrada logo depois de construir o host: esse aborto é esperado.
            if (falha is not HostAbortedException)
            {
                Falha = falha;
            }
        }

        public void OnNext(DiagnosticListener listener)
        {
            if (listener.Name == ListenerDoHost)
            {
                _assinaturasDosEventos.Add(listener.Subscribe(this));
            }
        }

        public void OnNext(KeyValuePair<string, object?> evento)
        {
            if (evento.Key == EventoHostConstruido && Environment.CurrentManagedThreadId == _threadDaExecucao)
            {
                Host = (IHost)evento.Value!;
                throw new HostAbortedException();
            }
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void Dispose()
        {
            _assinaturaDosListeners.Dispose();
            _assinaturasDosEventos.ForEach(static a => a.Dispose());
        }
    }
}
