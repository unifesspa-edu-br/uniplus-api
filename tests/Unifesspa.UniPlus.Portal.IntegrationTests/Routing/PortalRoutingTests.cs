namespace Unifesspa.UniPlus.Portal.IntegrationTests.Routing;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Reflection;

using AwesomeAssertions;

using Infrastructure;

using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Routing;

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

    [Fact(DisplayName = "Cada módulo expõe rotas sob seu prefixo api/{modulo}/")]
    public void RotasDeModulo_SaoNamespacedPorPrefixo()
    {
        EndpointDataSource dataSource =
            _factory.Services.GetRequiredService<EndpointDataSource>();

        RouteEndpoint[] endpoints = dataSource.Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<ControllerActionDescriptor>() != null)
            .ToArray();

        endpoints.Should().NotBeEmpty();

        endpoints.Should().OnlyContain(endpoint => HasExpectedRoutePrefix(endpoint));
    }

    private static bool HasExpectedRoutePrefix(RouteEndpoint endpoint)
    {
        string? prefix = ExpectedModulePrefix(endpoint);
        if (prefix is null) return false;

        string template = endpoint.RoutePattern.RawText ?? string.Empty;

        return template == prefix || template.StartsWith(
                    $"{prefix}/",
                    StringComparison.Ordinal);
    }

    private static string? ExpectedModulePrefix(RouteEndpoint endpoint)
    {
        ControllerActionDescriptor? actionDescriptor =
            endpoint.Metadata.GetMetadata<ControllerActionDescriptor>();

        Assembly? assembly = actionDescriptor?
            .ControllerTypeInfo
            .Assembly;

        return assembly is null ? null : $"api/{ApiModuleMetadata.GetRequiredName(assembly)}";
    }
}
