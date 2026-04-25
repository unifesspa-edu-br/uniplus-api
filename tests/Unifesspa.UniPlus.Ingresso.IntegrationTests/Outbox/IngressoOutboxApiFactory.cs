namespace Unifesspa.UniPlus.Ingresso.IntegrationTests.Outbox;

using System.Diagnostics.CodeAnalysis;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit IClassFixture<T> requires the fixture/factory type to be public.")]
public sealed class IngressoOutboxApiFactory(string connectionString) : ApiFactoryBase<Program>
{
    private readonly string _connectionString = connectionString;

    protected override IEnumerable<KeyValuePair<string, string?>> GetConfigurationOverrides() =>
    [
        new("ConnectionStrings:IngressoDb", _connectionString),
        new("Auth:Authority", "http://localhost/test-realm"),
        new("Auth:Audience", "uniplus"),
    ];
}
