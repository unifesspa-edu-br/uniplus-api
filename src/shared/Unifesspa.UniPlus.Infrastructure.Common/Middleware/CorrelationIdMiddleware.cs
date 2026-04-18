namespace Unifesspa.UniPlus.Infrastructure.Common.Middleware;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

using Serilog.Context;

public sealed class CorrelationIdMiddleware
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

    public async Task InvokeAsync(HttpContext context, ICorrelationIdAccessor accessor)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(accessor);

        string correlationId = ObterOuGerarCorrelationId(context);

        accessor.SetCorrelationId(correlationId);
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
            if (!string.IsNullOrWhiteSpace(existente) && existente.Length <= MaxCorrelationIdLength)
            {
                return existente;
            }
        }

        return Guid.NewGuid().ToString("D");
    }
}
