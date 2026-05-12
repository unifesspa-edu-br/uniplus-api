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
    /// Template de saída do <c>WriteTo.Console(...)</c> — inclui <c>TraceId=&lt;hex&gt;</c>
    /// e <c>SpanId=&lt;hex&gt;</c> no body do log line. Esse formato é contrato com o
    /// <c>derivedFields</c> do datasource Loki do Grafana
    /// (<c>uniplus-infra</c> PR #225), cuja regex
    /// <c>(?:traceID|trace_id|TraceId)["]?[=:]\s*["]?([a-fA-F0-9]{32})</c> captura o
    /// <c>TraceId</c> e gera o link <em>"Ver trace no Tempo"</em>.
    /// </summary>
    /// <remarks>
    /// <para><strong>Invariante</strong>: qualquer refactor que altere a posição ou
    /// formato de <c>TraceId=</c> ou <c>SpanId=</c> quebra o drill-down log↔trace no
    /// Grafana — defeito silencioso (CI verde, smoke visual quebrado). Sentinel test
    /// em <c>SerilogConfigurationTests.OutputTemplate_DeveConterTraceIdESpanId_*</c>
    /// trava regressões.</para>
    /// <para><strong>Posição do <c>{Properties:j}</c>:</strong> precede <c>TraceId</c>/<c>SpanId</c>
    /// para que as propriedades estruturadas (<c>CorrelationId</c>, <c>ServiceName</c>,
    /// custom enrichers) saiam em JSON inline antes do par chave-valor textual — facilita
    /// parsing por humanos durante troubleshooting e mantém a regra <c>TraceId=...</c>
    /// no fim da linha (próxima ao <c>NewLine</c>), padrão amplamente
    /// reconhecido por regex log scrapers.</para>
    /// </remarks>
    public const string ConsoleOutputTemplate =
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j} TraceId={TraceId} SpanId={SpanId}{NewLine}{Exception}";


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
            // TraceContextEnricher popula TraceId/SpanId a partir de Activity.Current
            // (ADR-0018) — invariante para o derivedFields do Loki no Grafana renderizar
            // o link "Ver trace no Tempo". Registrado APÓS PiiMaskingEnricher (preserva
            // ordem ADR-0011) e ANTES dos sinks (Console renderiza no outputTemplate;
            // OTLP popula via IncludedData.TraceIdField independentemente do enricher).
            .Enrich.With<TraceContextEnricher>();

        // ServiceNameEnricher é o segundo componente da ADR-0052: popula a propriedade
        // Serilog `ServiceName` em cada log event, visível em consumidores que não falam
        // OTel Resource (Console em dev, exportações JSON para auditoria). Registrado
        // APÓS o PiiMaskingEnricher e ANTES dos sinks — preserva a invariante ADR-0011
        // (PII mascarado antes de qualquer egress). Sem nomeServico, o enricher não é
        // registrado para evitar emitir property nula ou vazia.
        if (!string.IsNullOrWhiteSpace(nomeServico))
        {
            loggerConfiguration.Enrich.With(new ServiceNameEnricher(nomeServico));
        }

        loggerConfiguration.WriteTo.Console(
            outputTemplate: ConsoleOutputTemplate,
            formatProvider: CultureInfo.InvariantCulture);

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
