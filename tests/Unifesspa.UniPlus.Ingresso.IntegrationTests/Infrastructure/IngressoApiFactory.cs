namespace Unifesspa.UniPlus.Ingresso.IntegrationTests.Infrastructure;

using System.Diagnostics.CodeAnalysis;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit 2.x IClassFixture<T> requires the fixture type to be public.")]
public sealed class IngressoApiFactory : ApiFactoryBase<Program>
{
    protected override IEnumerable<KeyValuePair<string, string?>> GetConfigurationOverrides() =>
    [
        new("ConnectionStrings:IngressoDb", "Host=localhost;Port=5432;Database=uniplus_tests;Username=uniplus;Password=uniplus_dev"),
        new("Auth:Authority", "http://localhost/test-realm"),
        new("Auth:Audience", "uniplus"),
    ];
}
