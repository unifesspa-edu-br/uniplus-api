namespace Unifesspa.UniPlus.Application.Abstractions.Behaviors;

using System.Diagnostics;

using MediatR;

using Microsoft.Extensions.Logging;

public sealed partial class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        string requestName = typeof(TRequest).Name;
        LogProcessando(_logger, requestName);

        Stopwatch stopwatch = Stopwatch.StartNew();
        TResponse response = await next(cancellationToken).ConfigureAwait(false);
        stopwatch.Stop();

        LogConcluido(_logger, requestName, stopwatch.ElapsedMilliseconds);

        return response;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Processando {RequestName}")]
    private static partial void LogProcessando(ILogger logger, string requestName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Concluído {RequestName} em {ElapsedMs}ms")]
    private static partial void LogConcluido(ILogger logger, string requestName, long elapsedMs);
}
