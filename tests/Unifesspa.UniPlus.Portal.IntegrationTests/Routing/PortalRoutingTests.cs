namespace Unifesspa.UniPlus.Portal.IntegrationTests.Routing;

using System.Diagnostics.CodeAnalysis;
using System.Net;

using AwesomeAssertions;

using Infrastructure;

[Trait("Category", "Integration")]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit IClassFixture<T> exige tipo de teste público.")]
public sealed class PortalRoutingTests : IClassFixture<PortalApiFactory>
{
    private readonly PortalApiFactory _factory;

    public PortalRoutingTests(PortalApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PortalStandalone_PingRoute_UsesModulePrefix()
    {
        using HttpClient client = _factory.CreateClient();

        HttpResponseMessage prefixed = await client.GetAsync(
            new Uri("/api/portal/ping", UriKind.Relative));
        HttpResponseMessage unprefixed = await client.GetAsync(
            new Uri("/ping", UriKind.Relative));

        prefixed.StatusCode.Should().Be(HttpStatusCode.OK);
        unprefixed.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
