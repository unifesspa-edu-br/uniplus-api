namespace Unifesspa.UniPlus.Infrastructure.Core.Messaging.Middleware;

using System.Diagnostics;

using Microsoft.Extensions.Logging;

using Wolverine;
using Wolverine.Attributes;

/// <summary>
/// Middleware Wolverine que registra entrada e saída do handler com tempo de
/// execução. Roda no pipeline tanto de commands quanto de queries (registro
/// filtrado por <see cref="MessagingMiddlewarePolicies"/>) e provê o canal
/// canônico de logging estruturado para o backbone CQRS (ADR-022).
///
/// O caminho de saída é dividido em dois métodos para distinguir sucesso de
/// falha sem suprimir a exceção do pipeline ASP.NET:
/// <list type="bullet">
///   <item><description><c>Concluido</c> com <see cref="WolverineAfterAttribute"/>
///   roda <b>apenas em sucesso</b> e emite log <see cref="LogLevel.Information"/>;
///   é responsável por parar o <see cref="Stopwatch"/>.</description></item>
///   <item><description><c>Finally</c> com <see cref="WolverineFinallyAttribute"/>
///   roda em sucesso e em falha. Detecta falha pela invariante
///   <c>stopwatch.IsRunning == true</c> (sucesso já parou no <c>Concluido</c>) e
///   emite log <see cref="LogLevel.Warning"/> com a duração até a falha. Em
///   sucesso é no-op.</description></item>
/// </list>
/// </summary>
/// <remarks>
/// O <see cref="Stopwatch"/> retornado por <see cref="Before"/> é capturado pelo
/// code-gen do Wolverine como variável local da chain e injetado nos métodos
/// subsequentes por correspondência de tipo. A exceção do handler é re-lançada
/// pelo Wolverine após o <c>Finally</c> e flui normalmente até o
/// <c>GlobalExceptionMiddleware</c> da API.
///
/// Métodos não declaram <c>object message</c> de propósito: o code-gen do
/// Wolverine tenta resolver parâmetros não-message a partir do escopo da
/// chain, e tipar a mensagem como <c>object</c> conflita com a variável da
/// mensagem fortemente tipada já presente. Usar <see cref="Envelope.MessageType"/>
/// dá acesso ao alias estável (string) que Wolverine usa para roteamento sem
/// depender de <c>GetType()</c> em runtime.
/// </remarks>
public static partial class WolverineLoggingMiddleware
{
    [WolverineBefore]
    public static Stopwatch Before(
        Envelope envelope,
        ILogger<WolverineLoggingMiddlewareLogger> logger)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(logger);

        LogProcessando(logger, envelope.MessageType ?? envelope.Message?.GetType().Name ?? "(desconhecido)");
        return Stopwatch.StartNew();
    }

    [WolverineAfter]
    public static void Concluido(
        Envelope envelope,
        Stopwatch stopwatch,
        ILogger<WolverineLoggingMiddlewareLogger> logger)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(stopwatch);
        ArgumentNullException.ThrowIfNull(logger);

        stopwatch.Stop();
        LogConcluido(logger, envelope.MessageType ?? envelope.Message?.GetType().Name ?? "(desconhecido)", stopwatch.ElapsedMilliseconds);
    }

    [WolverineFinally]
    public static void Finally(
        Envelope envelope,
        Stopwatch stopwatch,
        ILogger<WolverineLoggingMiddlewareLogger> logger)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(stopwatch);
        ArgumentNullException.ThrowIfNull(logger);

        // Em sucesso o Concluido já parou o stopwatch — Finally é no-op.
        // Em falha o Concluido não rodou, então o stopwatch ainda está rodando
        // e Finally cobre o cleanup + telemetria de falha.
        if (!stopwatch.IsRunning)
        {
            return;
        }

        stopwatch.Stop();
        LogFalhou(logger, envelope.MessageType ?? envelope.Message?.GetType().Name ?? "(desconhecido)", stopwatch.ElapsedMilliseconds);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Processando {RequestName}")]
    private static partial void LogProcessando(ILogger logger, string requestName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Concluído {RequestName} em {ElapsedMs}ms")]
    private static partial void LogConcluido(ILogger logger, string requestName, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falhou {RequestName} em {ElapsedMs}ms")]
    private static partial void LogFalhou(ILogger logger, string requestName, long elapsedMs);
}

/// <summary>
/// Categoria de log estável para o middleware de logging Wolverine. Existe como
/// classe não estática para permitir <see cref="ILogger{T}"/> — classes static
/// não podem ser usadas como parâmetro de tipo. A categoria emitida no Serilog
/// fica <c>Unifesspa.UniPlus.Infrastructure.Core.Messaging.Middleware.WolverineLoggingMiddlewareLogger</c>.
/// </summary>
public sealed class WolverineLoggingMiddlewareLogger
{
    private WolverineLoggingMiddlewareLogger()
    {
    }
}
