namespace Unifesspa.UniPlus.Infrastructure.Common.Middleware;

using System.Collections.Immutable;
using System.Diagnostics;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>Middleware de logging estruturado de requests HTTP.</summary>
/// <remarks>
/// Mascara valores de query string via <see cref="QueryStringMasker"/>, mas não segmentos
/// de path — PII em path segments vaza em camadas anteriores ao middleware (nginx, WAF, CDN,
/// cabeçalho Referer). Rotas devem usar identificadores opacos (UUID), nunca dados sensíveis
/// em path. Ver unifesspa-edu-br/uniplus-docs#68.
/// </remarks>
public sealed partial class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;
    private readonly QueryStringMasker _masker;
    private readonly ImmutableArray<string> _prefixosSilenciados;

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

        // ImmutableArray (não FrozenSet): matching é prefix+boundary, não igualdade.
        _prefixosSilenciados = options.Value.PrefixosSilenciados
            .Select(NormalizarPrefixo)
            .Where(p => p.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToImmutableArray();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        long startTimestamp = Stopwatch.GetTimestamp();
        Exception? falha = null;

        // Capturamos para elevar o log a Error; `throw;` preserva stack para o
        // GlobalExceptionMiddleware. Sem isso, exception que escapa o pipeline
        // aparece como Information 200 no log (default do status code).
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
            double elapsedMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
            string method = context.Request.Method;
            string path = context.Request.Path.HasValue ? context.Request.Path.Value! : "/";
            string query = _masker.Mascarar(context.Request.QueryString);
            int statusCode = context.Response.StatusCode;

            if (falha is not null || statusCode >= 500)
            {
                LogRequestServerError(_logger, method, path, query, statusCode, elapsedMs, falha);
            }
            else if (statusCode >= 400)
            {
                LogRequestClientError(_logger, method, path, query, statusCode, elapsedMs);
            }
            else if (!DeveSilenciar(path))
            {
                LogRequestSucesso(_logger, method, path, query, statusCode, elapsedMs);
            }
        }
    }

    // Span evita alocação no hot path em requests com trailing slash.
    private bool DeveSilenciar(string path)
    {
        ReadOnlySpan<char> pathSpan = path.AsSpan();
        if (pathSpan.Length > 1 && pathSpan[^1] == '/')
        {
            pathSpan = pathSpan.TrimEnd('/');
        }

        foreach (string prefixo in _prefixosSilenciados)
        {
            if (pathSpan.Equals(prefixo, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (pathSpan.Length > prefixo.Length &&
                pathSpan.StartsWith(prefixo, StringComparison.OrdinalIgnoreCase) &&
                pathSpan[prefixo.Length] == '/')
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizarPrefixo(string valor) =>
        valor.Length > 1 && valor.EndsWith('/') ? valor.TrimEnd('/') : valor;

    [LoggerMessage(Level = LogLevel.Information, Message = "HTTP {Method} {Path}{Query} respondeu {StatusCode} em {ElapsedMs}ms")]
    private static partial void LogRequestSucesso(ILogger logger, string method, string path, string query, int statusCode, double elapsedMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "HTTP {Method} {Path}{Query} respondeu {StatusCode} em {ElapsedMs}ms")]
    private static partial void LogRequestClientError(ILogger logger, string method, string path, string query, int statusCode, double elapsedMs);

    // Exception no último parâmetro: convenção do source generator do LoggerMessage —
    // vai para LogEvent.Exception (stack estruturada nos sinks), não para o template.
    [LoggerMessage(Level = LogLevel.Error, Message = "HTTP {Method} {Path}{Query} respondeu {StatusCode} em {ElapsedMs}ms")]
    private static partial void LogRequestServerError(ILogger logger, string method, string path, string query, int statusCode, double elapsedMs, Exception? ex);
}
