namespace Unifesspa.UniPlus.Infrastructure.Core.IntegrationTests.Observability;

using System.Collections.Generic;
using System.Diagnostics;

using AwesomeAssertions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

using OpenTelemetry.Trace;

using Unifesspa.UniPlus.Infrastructure.Core.Observability;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

[Collection(OtelCollectorContainerFixture.CollectionName)]
public sealed class OpenTelemetryWiringTests(OtelCollectorContainerFixture collector)
{
    private const string ServiceName = "uniplus-test-otel-wiring";
    private const string EnvVarName = "OTEL_EXPORTER_OTLP_ENDPOINT";

    [Fact]
    public async Task AdicionarObservabilidade_PipelineEndToEnd_ExportaSpanRotuladoParaCollector()
    {
        // O OtlpExporter padrão lê o endpoint da env var OTEL_EXPORTER_OTLP_ENDPOINT
        // (sem opção de override via DI sem mudar a assinatura pública). Setamos
        // process-wide e restauramos no finally — xUnit não paraleliza dentro da
        // mesma collection, então não há race com outros testes.
        string? endpointAnterior = Environment.GetEnvironmentVariable(EnvVarName);
        try
        {
            Environment.SetEnvironmentVariable(EnvVarName, collector.GrpcEndpoint);

            ServiceCollection services = new();
            services.AddLogging();
            IConfiguration configuration = new ConfigurationBuilder().Build();
            IHostEnvironment environment = new TestHostEnvironment("Development");

            services.AdicionarObservabilidade(ServiceName, configuration, environment);

            await using ServiceProvider provider = services.BuildServiceProvider();

            // Resolver o TracerProvider força o startup do pipeline OTel — exporter
            // OTLP abre a conexão gRPC com o Collector neste ponto.
            TracerProvider? tracerProvider = provider.GetService<TracerProvider>();
            tracerProvider.Should().NotBeNull("AdicionarObservabilidade deve registrar um TracerProvider quando Observability:Enabled é o default true");

            // ActivitySource compartilha o nome do serviço — registrado em
            // AddSource(nomeServico) na pipeline. Sem isso, listeners do OTel
            // SDK ignoram os spans emitidos.
            using ActivitySource source = new(ServiceName);
            using (Activity? span = source.StartActivity("test-wiring-span"))
            {
                span.Should().NotBeNull("o sampler em Development é AlwaysOn — span não pode ser dropado");
                span!.SetTag("test.scenario", "wiring-end-to-end");
            }

            // ForceFlush garante que o batch processor envia imediatamente em vez
            // de esperar o intervalo (5s default). Timeout 5s é margem para gRPC
            // handshake + envio em runners CI mais lentos.
            tracerProvider!.ForceFlush(timeoutMilliseconds: 5_000).Should().BeTrue();

            // Buffer extra (200ms) para o Collector processar o batch recebido e
            // emitir no exporter debug. Heurística: na prática 50ms basta, 200ms
            // dá folga sem aumentar tempo do teste materialmente.
            await Task.Delay(TimeSpan.FromMilliseconds(200));

            string collectorOutput = await collector.GetLogsAsync();

            collectorOutput.Should().Contain("ResourceSpans", because: "o exporter debug do Collector escreve o nome do tipo OTLP recebido em stderr");
            collectorOutput.Should().Contain(ServiceName, because: $"o Resource attribute service.name precisa chegar até o Collector — confirma que ConfigureResource(...AddService(\"{ServiceName}\")) está wired");
            collectorOutput.Should().Contain("test-wiring-span", because: "o nome do span emitido localmente precisa aparecer no output do Collector — confirma que AddOtlpExporter() está exportando de fato");
            collectorOutput.Should().Contain(OpenTelemetryConfiguration.ServiceNamespaceResourceValue, because: "service.namespace=uniplus deve chegar no Resource exportado");
        }
        finally
        {
            Environment.SetEnvironmentVariable(EnvVarName, endpointAnterior);
        }
    }

    /// <summary>
    /// Stub minimal de <see cref="IHostEnvironment"/> — só popula
    /// <c>EnvironmentName</c> (única propriedade lida por
    /// <see cref="OpenTelemetryConfiguration.SelecionarSampler"/> e
    /// <see cref="OpenTelemetryConfiguration.AdicionarObservabilidade"/>).
    /// Evita NSubstitute aqui porque o test project não referencia o pacote
    /// (kept lean — IntegrationTests usa Testcontainers + AwesomeAssertions
    /// e nada além).
    /// </summary>
    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "Unifesspa.UniPlus.Infrastructure.Core.IntegrationTests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
