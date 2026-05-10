namespace Unifesspa.UniPlus.Infrastructure.Core.Observability;

using System.Collections.Generic;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

/// <summary>
/// Registra OpenTelemetry com instrumentações canônicas Uni+ (tracing + metrics)
/// e exporter OTLP para a stack LGTM institucional. ADR-0018.
/// </summary>
public static class OpenTelemetryConfiguration
{
    /// <summary>
    /// Nome canônico do <see cref="System.Diagnostics.ActivitySource"/> emitido
    /// pelo Wolverine. Registrado tanto em <c>WithTracing.AddSource</c> quanto em
    /// <c>WithMetrics.AddMeter</c> para que command/handler/outbox executions
    /// apareçam em Tempo (traces) e Prometheus (métricas) — ADR-0018.
    /// </summary>
    public const string WolverineActivityAndMeterName = "Wolverine";


    /// <summary>
    /// Toggle de configuração para observabilidade. Default <c>true</c>; quando
    /// <c>false</c>, nenhum <see cref="TracerProvider"/> ou <see cref="MeterProvider"/>
    /// é registrado. Cenário de uso: suites de teste HTTP-only sem Collector
    /// provisionado, troubleshooting onde a stack LGTM está fora do ar
    /// (degradação controlada). Mesma chave usada por <see cref="Logging.SerilogConfiguration"/>
    /// para condicionar o sink OTLP de logs.
    /// </summary>
    public const string EnabledConfigurationKey = "Observability:Enabled";

    /// <summary>
    /// Atributo Resource canônico do OTel para particionar telemetria entre
    /// Development/Staging/Production nos dashboards Grafana.
    /// </summary>
    public const string DeploymentEnvironmentResourceAttribute = "deployment.environment";

    /// <summary>
    /// Namespace canônico Uni+ — todas as APIs do projeto compartilham. Permite
    /// queries cross-service em PromQL/LogQL/TraceQL via
    /// <c>service_namespace="uniplus"</c>.
    /// </summary>
    public const string ServiceNamespaceResourceValue = "uniplus";

    /// <summary>
    /// Sampling ratio head-based para ambientes não-Development. 10% conforme
    /// ADR-0018; tail-based 100% para erros e latência alta é responsabilidade
    /// do <c>tail_sampling_processor</c> no Collector, não da API.
    /// </summary>
    public const double ProductionSamplingRatio = 0.1;

    /// <summary>
    /// Registra OpenTelemetry com instrumentações canônicas Uni+ e exporter OTLP.
    /// Endpoint OTLP é lido automaticamente pela env var
    /// <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> (default <c>http://localhost:4317</c>, gRPC).
    /// </summary>
    /// <remarks>
    /// <para><b>Tracing:</b> AspNetCore + EF Core + HttpClient + ActivitySource nominal
    /// (<paramref name="nomeServico"/>).</para>
    /// <para><b>Metrics:</b> AspNetCore + Runtime + HttpClient + Meter nominal +
    /// Wolverine (instrumentação built-in via <c>System.Diagnostics.Metrics.Meter</c>
    /// nativo do framework — ADR-0018).</para>
    /// <para><b>Sampler:</b> <see cref="AlwaysOnSampler"/> em Development,
    /// <see cref="ParentBasedSampler"/> com <see cref="TraceIdRatioBasedSampler"/>
    /// (10%) nos demais.</para>
    /// </remarks>
    /// <param name="services">A coleção de serviços.</param>
    /// <param name="nomeServico">Nome canônico do serviço (ex.: <c>uniplus-selecao</c>),
    /// usado como <c>service.name</c> no Resource e como nome da
    /// <c>ActivitySource</c>/<c>Meter</c> nominal.</param>
    /// <param name="configuration">Configuração da aplicação.</param>
    /// <param name="environment">Ambiente de hosting — define o sampler e popula
    /// <c>deployment.environment</c> no Resource.</param>
    /// <returns>A própria <paramref name="services"/> para encadeamento fluente.</returns>
    public static IServiceCollection AdicionarObservabilidade(
        this IServiceCollection services,
        string nomeServico,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(nomeServico);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        bool enabled = configuration.GetValue(EnabledConfigurationKey, defaultValue: true);
        if (!enabled)
        {
            return services;
        }

        Sampler sampler = SelecionarSampler(environment);

        IEnumerable<KeyValuePair<string, object>> resourceAttributes = new[]
        {
            new KeyValuePair<string, object>(
                DeploymentEnvironmentResourceAttribute,
                environment.EnvironmentName),
        };

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName: nomeServico, serviceNamespace: ServiceNamespaceResourceValue)
                .AddAttributes(resourceAttributes))
            .WithTracing(tracing => tracing
                .SetSampler(sampler)
                .AddSource(nomeServico)
                .AddSource(WolverineActivityAndMeterName)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddEntityFrameworkCoreInstrumentation()
                .AddOtlpExporter())
            .WithMetrics(metrics => metrics
                .AddMeter(nomeServico)
                .AddMeter(WolverineActivityAndMeterName)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddOtlpExporter());

        return services;
    }

    /// <summary>
    /// Seleciona o sampler conforme ambiente: <see cref="AlwaysOnSampler"/>
    /// em <c>Development</c> (debugging local — todos os spans), e
    /// <see cref="ParentBasedSampler"/> com <see cref="TraceIdRatioBasedSampler"/>
    /// em <see cref="ProductionSamplingRatio"/> (10%) nos demais ambientes —
    /// ADR-0018 head-based sampling. Tail-based 100% para erro/latência alta
    /// é responsabilidade do <c>tail_sampling_processor</c> no Collector.
    /// </summary>
    /// <remarks>
    /// Extraído como <c>internal static</c> exatamente para tornar a regra de
    /// seleção testável sem precisar inspecionar o <c>TracerProvider</c> via
    /// reflection — <c>InternalsVisibleTo</c> em <c>Infrastructure.Core.csproj</c>
    /// expõe esta API para <c>Unifesspa.UniPlus.Infrastructure.Core.UnitTests</c>.
    /// </remarks>
    internal static Sampler SelecionarSampler(IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        return environment.IsDevelopment()
            ? new AlwaysOnSampler()
            : new ParentBasedSampler(new TraceIdRatioBasedSampler(ProductionSamplingRatio));
    }
}
