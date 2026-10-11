namespace Unifesspa.UniPlus.Portal.IntegrationTests.Infrastructure;

using System.Diagnostics.CodeAnalysis;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Portal.API;

/// <summary>
/// Portal com os endereços das origens em branco — inclusive os que a configuração de
/// desenvolvimento traria —, para conferir que o processo recusa subir sem eles.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit IClassFixture<T> exige fixture pública.")]
public sealed class PortalSemOrigensApiFactory : ApiFactoryBase<PortalApiAssemblyMarker>
{
    private const string FakeConnectionString =
        "Host=integration-not-real;Database=portal;Username=u;Password=p";

    protected override IEnumerable<KeyValuePair<string, string?>> GetConfigurationOverrides() =>
    [
        new("ConnectionStrings:PortalDb", FakeConnectionString),
        new("Kafka:BootstrapServers", string.Empty),
        new("Auth:Authority", "http://localhost/test-realm"),
        new("Auth:Audience", "uniplus"),
        new("Origens:SelecaoBaseUrl", string.Empty),
        new("Origens:PublicacoesBaseUrl", string.Empty),
    ];
}
