namespace Unifesspa.UniPlus.Infrastructure.Common.Middleware;

using System.Text.RegularExpressions;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

using Serilog.Context;

public sealed partial class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";
    public const string LogContextProperty = "CorrelationId";
    public const int MaxCorrelationIdLength = 128;

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(next);
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ICorrelationIdWriter writer)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(writer);

        string correlationId = ObterOuGerarCorrelationId(context);

        writer.SetCorrelationId(correlationId);
        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty(LogContextProperty, correlationId))
        {
            await _next(context).ConfigureAwait(false);
        }
    }

    private static string ObterOuGerarCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out StringValues valor))
        {
            string existente = valor.ToString();
            if (FormatoValido().IsMatch(existente))
            {
                return existente;
            }
        }

        return Guid.NewGuid().ToString("D");
    }

    // Restringe valores aceitos do cliente a ASCII imprimível [A-Za-z0-9\-_.],
    // 1..128 chars. Rejeita controle (\r\n, NULL), whitespace e não-ASCII —
    // previne log injection e poluição de dashboards estruturados downstream.
    // O upper bound do quantificador deve espelhar MaxCorrelationIdLength.
    [GeneratedRegex(@"^[A-Za-z0-9\-_.]{1,128}$")]
    private static partial Regex FormatoValido();
}
