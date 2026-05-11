namespace Unifesspa.UniPlus.Infrastructure.Core.Logging;

using Serilog.Core;
using Serilog.Events;

/// <summary>
/// Enricher Serilog que adiciona a propriedade <c>ServiceName</c> em cada
/// <see cref="LogEvent"/> emitido pelo pipeline da API. Implementa o segundo
/// componente da ADR-0052 (rastreabilidade cross-service).
/// </summary>
/// <remarks>
/// <para>O valor populado por este enricher é o mesmo string consumido pelo
/// <c>Resource</c> do OpenTelemetry SDK em
/// <see cref="Observability.OpenTelemetryConfiguration.AdicionarObservabilidade"/>
/// — duplicação intencional e validada por construção via
/// <see cref="Observability.UniPlusServiceNames"/>. A propriedade Serilog é
/// <c>ServiceName</c> (PascalCase, alinhada com <c>CorrelationId</c>); o
/// atributo OTel Resource é <c>service.name</c> (dot-notation, semantic
/// conventions). O sink Serilog OTLP traduz a property para o Resource attr no
/// protocolo wire — não há divergência operacional.</para>
/// <para>Consumidores nomeados de <c>ServiceName</c> como property Serilog
/// (fora do label <c>service_name</c> do Loki): <c>docker logs</c> /
/// <c>dotnet run</c> em desenvolvimento; exportações JSON de auditoria
/// (TCU, jurídico) que não falam OTel Resource. Caso esses consumidores deixem
/// de existir, o enricher pode ser removido em ADR de revisão sem impacto no
/// pipeline OTLP — a Resource attr permanece como verdade canônica.</para>
/// <para>O enricher é registrado em <see cref="SerilogConfiguration.ConfigurarSerilog(Serilog.LoggerConfiguration, Microsoft.Extensions.Configuration.IConfiguration, string?)"/>
/// APÓS o <see cref="PiiMaskingEnricher"/> e ANTES dos sinks Console/OTLP —
/// preservando a ordem da ADR-0011 (PII mascarado antes de qualquer egress).</para>
/// </remarks>
public sealed class ServiceNameEnricher : ILogEventEnricher
{
    /// <summary>
    /// Nome da propriedade Serilog populada em cada log event. PascalCase
    /// alinhado com <see cref="Middleware.CorrelationIdMiddleware.LogContextProperty"/>.
    /// </summary>
    public const string PropertyName = "ServiceName";

    private readonly LogEventProperty _property;

    /// <param name="serviceName">Nome canônico do serviço — tipicamente um valor
    /// de <see cref="Observability.UniPlusServiceNames"/>. Não pode ser vazio.</param>
    public ServiceNameEnricher(string serviceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        // Cache a property como ScalarValue imutável — emitida em todo evento sem realocação.
        // O Serilog suporta property reuse desde que o valor seja imutável (ScalarValue é).
        _property = new LogEventProperty(PropertyName, new ScalarValue(serviceName));
    }

    /// <inheritdoc/>
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        logEvent.AddOrUpdateProperty(_property);
    }
}
