namespace Unifesspa.UniPlus.Host.IntegrationTests.Email;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using OpenTelemetry;
using OpenTelemetry.Metrics;

using Unifesspa.UniPlus.Host;

/// <summary>
/// Host com o pipeline de métricas do OpenTelemetry ligado a um exportador em memória, um por classe de teste: o host
/// de teste desliga a observabilidade, e subir um host por teste acumularia memória.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige que a fixture de classe seja pública.")]
public sealed class HostComColetaDeMetricasDeEmail : IDisposable
{
    private readonly Lock _trava = new();
    private WebApplicationFactory<HostAssemblyMarker>? _host;

    internal ExportadorDeMetricasEmMemoria Exportador { get; } = new();

    internal WebApplicationFactory<HostAssemblyMarker> Obter(MonolitoComMailpitFixture fixture)
    {
        lock (_trava)
        {
            return _host ??= fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(servicos =>
                servicos.AddOpenTelemetry().WithMetrics(metricas => metricas.AddReader(new BaseExportingMetricReader(Exportador)))));
        }
    }

    /// <summary>Exporta agora o que o pipeline coletou e devolve as medidas dessa exportação.</summary>
    internal IReadOnlyList<MedidaExportada> Exportar(MonolitoComMailpitFixture fixture)
    {
        Obter(fixture).Services.GetRequiredService<MeterProvider>().ForceFlush();

        return Exportador.UltimaExportacao;
    }

    public void Dispose()
    {
        _host?.Dispose();
        Exportador.Dispose();
    }
}
