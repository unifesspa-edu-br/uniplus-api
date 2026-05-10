namespace Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

using System.Text;

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;

/// <summary>
/// Sobe um <c>otel/opentelemetry-collector-contrib</c> via Testcontainers configurado com
/// receivers OTLP (gRPC + HTTP) e exporter <c>debug</c> com verbosity <c>detailed</c> —
/// permite que testes E2E afirmem que spans/metrics/logs chegaram ao Collector inspecionando
/// o stdout do container via <see cref="GetStdoutAsync"/>. Compartilhada via
/// <c>[Collection("OtelCollector")]</c>; cada assembly que usa declara sua própria
/// <c>[CollectionDefinition]</c> com o mesmo nome.
/// </summary>
/// <remarks>
/// <para>O config YAML é montado inline via <c>WithResourceMapping</c> (bytes em memória, sem
/// arquivo temp em disco) — único exporter habilitado é <c>debug</c>, o que garante isolamento
/// (não há rede saindo do Collector para Loki/Tempo/Prometheus em ambientes de CI).</para>
/// <para>A imagem é fixada em uma tag estável da família <c>0.x</c>; alinhar com a versão
/// usada pelo Collector institucional reduz superfície de surpresa quando o exporter ou
/// receiver evoluir. Atualização vem via Renovate (uniplus-api#366).</para>
/// </remarks>
public sealed class OtelCollectorContainerFixture : IAsyncLifetime
{
    public const string Image = "otel/opentelemetry-collector-contrib:0.117.0";
    public const string CollectionName = "OtelCollector";

    private const ushort GrpcPort = 4317;
    private const ushort HttpPort = 4318;
    private const string ConfigPath = "/etc/otelcol-contrib/config.yaml";

    private readonly IContainer _container;

    public OtelCollectorContainerFixture()
    {
        byte[] config = Encoding.UTF8.GetBytes(BuildMinimalConfig());

        _container = new ContainerBuilder(Image)
            .WithPortBinding(GrpcPort, true)
            .WithPortBinding(HttpPort, true)
            .WithResourceMapping(config, ConfigPath)
            .WithCommand("--config", ConfigPath)
            .WithWaitStrategy(
                Wait.ForUnixContainer()
                    .UntilMessageIsLogged("Everything is ready. Begin running and processing data."))
            .Build();
    }

    /// <summary>
    /// Endpoint OTLP gRPC no formato <c>http://host:port</c> (apto a alimentar
    /// <c>OTEL_EXPORTER_OTLP_ENDPOINT</c>).
    /// </summary>
    public string GrpcEndpoint =>
        $"http://{_container.Hostname}:{_container.GetMappedPublicPort(GrpcPort)}";

    /// <summary>
    /// Lê stdout + stderr acumulados do container concatenados em uma única string.
    /// Usado pelos testes para afirmar que spans/metrics/logs foram exportados
    /// (procuram tokens como <c>"ResourceSpans"</c>, <c>"service.name"</c>).
    /// </summary>
    /// <remarks>
    /// O <c>otel/opentelemetry-collector-contrib</c> emite o output do exporter
    /// <c>debug</c> em <c>stderr</c> (não <c>stdout</c>) — incluímos os dois aqui
    /// para que a fixture funcione consistentemente independente de qual stream
    /// o Collector escolher emitir em versões futuras.
    /// </remarks>
    public async Task<string> GetLogsAsync()
    {
        (string Stdout, string Stderr) logs = await _container.GetLogsAsync().ConfigureAwait(false);
        return string.Concat(logs.Stdout, logs.Stderr);
    }

    public Task InitializeAsync() => _container.StartAsync();

    public async Task DisposeAsync() =>
        await _container.DisposeAsync().ConfigureAwait(false);

    private static string BuildMinimalConfig() => """
        receivers:
          otlp:
            protocols:
              grpc:
                endpoint: 0.0.0.0:4317
              http:
                endpoint: 0.0.0.0:4318

        processors:
          batch:
            timeout: 100ms

        exporters:
          debug:
            verbosity: detailed

        service:
          pipelines:
            traces:
              receivers: [otlp]
              processors: [batch]
              exporters: [debug]
            metrics:
              receivers: [otlp]
              processors: [batch]
              exporters: [debug]
            logs:
              receivers: [otlp]
              processors: [batch]
              exporters: [debug]
        """;
}
