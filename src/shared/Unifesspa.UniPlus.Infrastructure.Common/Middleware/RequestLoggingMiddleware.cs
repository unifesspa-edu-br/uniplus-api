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
        Exception? falha = null;

        // try/catch/throw + finally: capturamos a exception apenas como
        // contexto para o log (mantendo stack trace via `throw;`) e a
        // deixamos propagar para o GlobalExceptionMiddleware decidir a
        // resposta. Sem essa captura, uma exception que escapasse daria
        // um log Information com status 200 (default) — mascarando falha
        // como sucesso.
        try
        {
            await _next(context).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            falha = ex;
            throw;
        }
        finally
        {
            long elapsedMs = (long)Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
            string method = context.Request.Method;
            string path = context.Request.Path.HasValue ? context.Request.Path.Value! : "/";
            string query = _masker.Mascarar(context.Request.QueryString);
            int statusCode = context.Response.StatusCode;

            // Presença de exception força nível Error independentemente do
            // status code observado — evita que uma falha silenciosa (status
            // não alterado) apareça como sucesso no log. Severidade
            // proporcional à categoria HTTP: 5xx é falha do servidor,
            // 4xx sinaliza problema no lado do cliente e demais respostas são
            // tráfego operacional normal. Paths de infraestrutura (health,
            // metrics) são silenciados quando bem-sucedidos para não saturar
            // observabilidade com probes de liveness/readiness; respostas de
            // erro continuam sendo reportadas porque sinalizam problema real.
            if (falha is not null || statusCode >= 500)
            {
                LogRequestServerError(_logger, method, path, query, statusCode, elapsedMs, falha);
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

    // Exception é o último parâmetro (convenção do source generator de
    // LoggerMessage): o gerador reconhece o tipo e emite no LogEvent.Exception
    // em vez de no template de mensagem — preserva stack trace estruturada
    // nos sinks sem polui-lo com a representação textual.
    [LoggerMessage(Level = LogLevel.Error, Message = "HTTP {Method} {Path}{Query} respondeu {StatusCode} em {ElapsedMs}ms")]
    private static partial void LogRequestServerError(ILogger logger, string method, string path, string query, int statusCode, long elapsedMs, Exception? ex);
}
