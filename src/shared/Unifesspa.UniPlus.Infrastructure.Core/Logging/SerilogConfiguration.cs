namespace Unifesspa.UniPlus.Infrastructure.Core.Logging;

using System.Collections.Generic;
using System.Globalization;

using Microsoft.Extensions.Configuration;

using Serilog;
using Serilog.Events;
using Serilog.Sinks.OpenTelemetry;

using Unifesspa.UniPlus.Infrastructure.Core.Observability;

/// <summary>
/// Configuração canônica do pipeline Serilog Uni+: lê <c>appsettings</c>, override
/// de Microsoft/EF para Warning, <c>FromLogContext</c>, <see cref="PiiMaskingEnricher"/>
/// (ADR-0011), Console sink. Quando <c>Observability:Enabled</c> não está desligado,
/// adiciona o sink OTLP gRPC (ADR-0018) — logs fluem ao Collector → Loki com correlação
/// <c>traceId</c>/<c>spanId</c> automática por evento.
/// </summary>
public static class SerilogConfiguration
{
    /// <summary>
    /// Sobrecarga sem nome de serviço — mantida para callers que ainda não precisam
    /// rotular logs com <c>service.name</c>/<c>service.namespace</c> no Loki.
    /// O sink OTLP é registrado mesmo assim (quando o toggle está ativo); apenas
    /// não popula <c>ResourceAttributes</c>, deixando o sink usar suas próprias
    /// inferências (env vars padrão OTel, se presentes).
    /// </summary>
    public static LoggerConfiguration ConfigurarSerilog(
        this LoggerConfiguration loggerConfiguration,
        IConfiguration configuration)
        => loggerConfiguration.ConfigurarSerilog(configuration, nomeServico: null);

    /// <summary>
    /// Configura o pipeline com <c>service.name</c>/<c>service.namespace</c> aplicados
    /// ao Resource do sink OTLP — habilita queries LogQL como
    /// <c>{service_name="uniplus-selecao"}</c>.
    /// </summary>
    /// <remarks>
    /// <para>O Console sink é sempre preservado para que <c>docker logs</c> continue
    /// útil em bootstrap debugging.</para>
    /// <para><see cref="PiiMaskingEnricher"/> fica antes dos sinks por construção:
    /// enrichers executam antes dos sinks no pipeline Serilog. CPF é mascarado
    /// antes de qualquer egress (Console ou OTLP) — ADR-0011.</para>
    /// <para>Endpoint OTLP é lido pela env var <c>OTEL_EXPORTER_OTLP_ENDPOINT</c>
    /// (default <c>http://localhost:4317</c>). Os atributos <c>service.*</c> são
    /// injetados aqui porque o pipeline Serilog→OTLP é independente do pipeline
    /// OTel SDK (<see cref="OpenTelemetryConfiguration.AdicionarObservabilidade"/>) —
    /// duplicação intencional para evitar drift entre logs e traces no Loki/Tempo.</para>
    /// </remarks>
    /// <param name="loggerConfiguration">Configuração base do Serilog.</param>
    /// <param name="configuration">Configuração da aplicação — fornece toggle
    /// <see cref="OpenTelemetryConfiguration.EnabledConfigurationKey"/> e overrides
    /// de level via <c>Serilog</c> section.</param>
    /// <param name="nomeServico">Nome canônico do serviço para rotular o sink OTLP
    /// (<c>service.name</c> + <c>service.namespace</c>). Quando <c>null</c>, sink
    /// é registrado sem ResourceAttributes.</param>
    public static LoggerConfiguration ConfigurarSerilog(
        this LoggerConfiguration loggerConfiguration,
        IConfiguration configuration,
        string? nomeServico)
    {
        ArgumentNullException.ThrowIfNull(loggerConfiguration);
        ArgumentNullException.ThrowIfNull(configuration);

        loggerConfiguration
            .ReadFrom.Configuration(configuration)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.With<PiiMaskingEnricher>()
            .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture);

        bool observabilidadeAtivada = configuration.GetValue(
            OpenTelemetryConfiguration.EnabledConfigurationKey,
            defaultValue: true);

        if (observabilidadeAtivada)
        {
            loggerConfiguration.WriteTo.OpenTelemetry(options =>
            {
                options.Protocol = OtlpProtocol.Grpc;
                options.IncludedData =
                    IncludedData.TraceIdField
                    | IncludedData.SpanIdField
                    | IncludedData.MessageTemplateTextAttribute;

                if (!string.IsNullOrWhiteSpace(nomeServico))
                {
                    options.ResourceAttributes = new Dictionary<string, object>
                    {
                        ["service.name"] = nomeServico,
                        ["service.namespace"] = OpenTelemetryConfiguration.ServiceNamespaceResourceValue,
                    };
                }
            });
        }

        return loggerConfiguration;
    }
}
