namespace Unifesspa.UniPlus.Geo.IntegrationTests.Infrastructure;

using System.Diagnostics.CodeAnalysis;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// Factory leve para os testes que só exercitam o pipeline HTTP de documentação
/// (<c>/openapi/geo.json</c>) — connection string sintética, sem Postgres real.
/// <c>DisableWolverineRuntimeForTests</c> e a remoção de health checks de infra
/// ficam no default do <see cref="ApiFactoryBase{T}"/> (não há banco a tocar).
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit IClassFixture<T> exige tipo público para a fixture compartilhada.")]
public sealed class GeoOpenApiFactory : ApiFactoryBase<Program>
{
    protected override IEnumerable<KeyValuePair<string, string?>> GetConfigurationOverrides() =>
    [
        new("ConnectionStrings:GeoDb", "Host=localhost;Port=5432;Database=uniplus_tests;Username=uniplus;Password=uniplus_dev"),
        new("Auth:Authority", "http://localhost/test-realm"),
        new("Auth:Audience", "uniplus"),
        // Sem worker do ETL: a connection string é sintética (não há banco a tocar).
        new("Geo:Etl:WorkerHabilitado", "false"),
    ];
}
