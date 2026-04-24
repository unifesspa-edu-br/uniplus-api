namespace Unifesspa.UniPlus.Ingresso.IntegrationTests.Infrastructure;

using Unifesspa.UniPlus.IntegrationTests.Shared.Hosting;

#pragma warning disable CA1515 // Fixture needs to be visible to xUnit.
public sealed class IngressoApiFactory : ApiFactoryBase<Program>
#pragma warning restore CA1515
{
    protected override IEnumerable<KeyValuePair<string, string?>> GetConfigurationOverrides() =>
    [
        new("ConnectionStrings:IngressoDb", "Host=localhost;Port=5432;Database=uniplus_tests;Username=uniplus;Password=uniplus_dev"),
        new("Auth:Authority", "http://localhost/test-realm"),
        new("Auth:Audience", "uniplus"),
    ];
}
