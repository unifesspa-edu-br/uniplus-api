namespace Unifesspa.UniPlus.Infrastructure.Core.Logging;

using System.Diagnostics;

using Serilog.Core;
using Serilog.Events;

/// <summary>
/// Enricher Serilog que popula <c>TraceId</c> e <c>SpanId</c> em cada
/// <see cref="LogEvent"/> a partir de <see cref="Activity.Current"/>. Mantém
/// invariante do pipeline ADR-0018: logs emitidos por sinks textuais (Console,
/// JSON export) carregam os identificadores W3C do trace ativo, permitindo que
/// o <c>derivedFields</c> do datasource Loki do Grafana (regex
/// <c>(?:traceID|trace_id|TraceId)["]?[=:]\s*["]?([a-fA-F0-9]{32})</c>)
/// reconstrua o link clicável <em>"Ver trace no Tempo"</em>.
/// </summary>
/// <remarks>
/// <para><strong>Por que custom em vez de <c>Serilog.Enrichers.Span</c>?</strong>
/// Zero dependência adicional, mesmo pattern do
/// <see cref="ServiceNameEnricher"/> (PR #411 / ADR-0052), e total controle do
/// formato emitido — alinhado com a regex do derivedFields configurada no
/// PR <c>uniplus-infra#225</c>.</para>
/// <para><strong>Comportamento quando <see cref="Activity.Current"/> é null</strong>
/// (logs antes do <c>app.UseRouting()</c>, ou hosted services sem instrumentação
/// OTel): emite <c>TraceId</c>/<c>SpanId</c> como string vazia. Output do Console
/// fica como <c>TraceId= SpanId=</c> — a regex do <c>derivedFields</c> exige
/// 32 hex chars após o <c>=</c>, então não casa, não gera link
/// spurious para um trace que não existe. Mantemos as propriedades sempre
/// presentes (mesmo que vazias) para preservar a estrutura do log JSON em
/// exportações e evitar que o template Console renderize <c>{TraceId}</c>
/// literal quando a property estaria ausente.</para>
/// <para><strong>Ordem do pipeline preservada</strong> (ADR-0011): registrado
/// APÓS o <see cref="PiiMaskingEnricher"/> e ANTES dos sinks. Os valores
/// emitidos são opacos (hex hash) sem PII — não exigem masking
/// — mas a precedence é mantida por consistência da pipeline.</para>
/// </remarks>
public sealed class TraceContextEnricher : ILogEventEnricher
{
    /// <summary>Nome da propriedade Serilog correspondente ao <c>TraceId</c> W3C.</summary>
    public const string TraceIdPropertyName = "TraceId";

    /// <summary>Nome da propriedade Serilog correspondente ao <c>SpanId</c> W3C.</summary>
    public const string SpanIdPropertyName = "SpanId";

    /// <inheritdoc/>
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        // Pattern do projeto (espelha PiiMaskingEnricher e ServiceNameEnricher):
        // valores escalares simples são construídos via `new LogEventProperty(...)`
        // direto, sem passar pelo `propertyFactory` — este existe para destructuring
        // de objetos complexos (não é o caso de TraceId/SpanId strings opacas).
        Activity? current = Activity.Current;
        string traceId = current?.TraceId.ToString() ?? string.Empty;
        string spanId = current?.SpanId.ToString() ?? string.Empty;

        logEvent.AddOrUpdateProperty(new LogEventProperty(TraceIdPropertyName, new ScalarValue(traceId)));
        logEvent.AddOrUpdateProperty(new LogEventProperty(SpanIdPropertyName, new ScalarValue(spanId)));
    }
}
