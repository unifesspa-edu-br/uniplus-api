namespace Unifesspa.UniPlus.Infrastructure.Common.Middleware;

using System.Collections.Frozen;
using System.Diagnostics;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public sealed partial class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;
    private readonly QueryStringMasker _masker;
    private readonly FrozenSet<string> _pathsSilenciados;

    public RequestLoggingMiddleware(
        RequestDelegate next,
        ILogger<RequestLoggingMiddleware> logger,
        QueryStringMasker masker,
        IOptions<RequestLoggingOptions> options)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(masker);
        ArgumentNullException.ThrowIfNull(options);
        _next = next;
        _logger = logger;
        _masker = masker;
        _pathsSilenciados = FrozenSet.ToFrozenSet(
            options.Value.PathsSilenciados,
            StringComparer.OrdinalIgnoreCase);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        long startTimestamp = Stopwatch.GetTimestamp();

        // try/finally garante registro do log mesmo quando um middleware
        // downstream (ex.: GlobalExceptionMiddleware) modifica o status code
        // durante o desenrolar da pilha. Não capturamos a exception — apenas
        // a deixamos propagar após registrar.
        try
        {
            await _next(context).ConfigureAwait(false);
        }
        finally
        {
            long elapsedMs = (long)Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
            string method = context.Request.Method;
            string path = context.Request.Path.HasValue ? context.Request.Path.Value! : "/";
            string query = _masker.Mascarar(context.Request.QueryString);
            int statusCode = context.Response.StatusCode;

            // Severidade proporcional à categoria HTTP: 5xx é falha do servidor,
            // 4xx sinaliza problema no lado do cliente e demais respostas são
            // tráfego operacional normal. Paths de infraestrutura (health,
            // metrics) são silenciados quando bem-sucedidos para não saturar
            // observabilidade com probes de liveness/readiness; respostas de
            // erro continuam sendo reportadas porque sinalizam problema real.
            if (statusCode >= 500)
            {
                LogRequestServerError(_logger, method, path, query, statusCode, elapsedMs);
            }
            else if (statusCode >= 400)
            {
                LogRequestClientError(_logger, method, path, query, statusCode, elapsedMs);
            }
            else if (!_pathsSilenciados.Contains(path))
            {
                LogRequestSucesso(_logger, method, path, query, statusCode, elapsedMs);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "HTTP {Method} {Path}{Query} respondeu {StatusCode} em {ElapsedMs}ms")]
    private static partial void LogRequestSucesso(ILogger logger, string method, string path, string query, int statusCode, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "HTTP {Method} {Path}{Query} respondeu {StatusCode} em {ElapsedMs}ms")]
    private static partial void LogRequestClientError(ILogger logger, string method, string path, string query, int statusCode, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Error, Message = "HTTP {Method} {Path}{Query} respondeu {StatusCode} em {ElapsedMs}ms")]
    private static partial void LogRequestServerError(ILogger logger, string method, string path, string query, int statusCode, long elapsedMs);
}
