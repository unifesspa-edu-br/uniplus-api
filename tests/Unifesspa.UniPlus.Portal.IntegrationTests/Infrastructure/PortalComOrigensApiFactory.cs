namespace Unifesspa.UniPlus.Portal.IntegrationTests.Infrastructure;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Hosting;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Portal.API;

/// <summary>
/// Sobe a Portal com as origens configuradas — endereços que nunca são chamados nos testes que a
/// usam: eles conferem a composição do contêiner, não a rede.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit IClassFixture<T> exige fixture pública.")]
public sealed class PortalComOrigensApiFactory : ApiFactoryBase<PortalApiAssemblyMarker>
{
    private const string FakeConnectionString =
        "Host=integration-not-real;Database=portal;Username=u;Password=p";

    protected override IEnumerable<KeyValuePair<string, string?>> GetConfigurationOverrides() =>
    [
        new("ConnectionStrings:PortalDb", FakeConnectionString),
        new("Kafka:BootstrapServers", string.Empty),
        new("Auth:Authority", "http://localhost/test-realm"),
        new("Auth:Audience", "uniplus"),
    ];

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        base.ConfigureWebHost(builder);

        // O Program decide registrar as origens lendo a configuração antes de construir o host;
        // os overrides aplicados na construção chegariam tarde. A configuração de host chega a tempo.
        builder.UseSetting("Origens:SelecaoBaseUrl", "http://uniplus-api.integration-not-real:8080");
        builder.UseSetting("Origens:PublicacoesBaseUrl", "http://uniplus-api.integration-not-real:8080");
    }
}
