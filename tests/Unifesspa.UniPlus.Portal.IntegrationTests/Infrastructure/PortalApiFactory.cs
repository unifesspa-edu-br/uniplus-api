namespace Unifesspa.UniPlus.Portal.IntegrationTests.Infrastructure;

using System.Diagnostics.CodeAnalysis;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Portal.API;

[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit IClassFixture<T> exige fixture pública.")]
public sealed class PortalApiFactory : ApiFactoryBase<PortalApiAssemblyMarker>
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
}
