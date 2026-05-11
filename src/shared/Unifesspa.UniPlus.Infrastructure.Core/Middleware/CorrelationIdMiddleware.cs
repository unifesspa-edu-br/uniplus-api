namespace Unifesspa.UniPlus.Infrastructure.Core.Middleware;

using System.Diagnostics;
using System.Text.RegularExpressions;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

using Serilog.Context;

public sealed partial class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";
    public const string LogContextProperty = "CorrelationId";
    public const int MaxCorrelationIdLength = 128;

    /// <summary>
    /// Nome do span attribute (Activity tag) que carrega o correlation_id no Tempo.
    /// Lido por <c>derivedFields</c> do Loki datasource em Grafana para o drill-down
    /// log → trace, fechando o ciclo log ↔ trace ↔ dashboard (ADR-0018).
    /// </summary>
    public const string ActivityTagName = "correlation_id";

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

        // Propagar correlation_id como Activity span attribute habilita o
        // drill-down Loki → Tempo via derivedFields no Grafana (ADR-0018) e
        // fecha o ciclo log ↔ trace. Activity.Current pode ser null fora do
        // request pipeline com OTel wired (ex.: testes unitários sem listener);
        // o ?. mantém o middleware safe nesse cenário.
        Activity.Current?.SetTag(ActivityTagName, correlationId);

        // Postergar a escrita do header até o início do flush da resposta
        // garante que o valor sobreviva a qualquer mutação feita por
        // middlewares/filtros downstream e seja comprometido logo antes do
        // status line ser enviado ao cliente.
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

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
    //
    // Pattern exposto como `internal const` para ser consumido também pelo
    // CorrelationIdEnvelopeMiddleware (Wolverine, ADR-0052): ambos os boundaries
    // (HTTP e Kafka) precisam validar com a MESMA regra ou a invariante de wire
    // format uniforme quebra silenciosamente em refactors.
    internal const string FormatoValidoPattern = @"^[A-Za-z0-9\-_.]{1,128}$";

    [GeneratedRegex(FormatoValidoPattern)]
    private static partial Regex FormatoValido();
}
