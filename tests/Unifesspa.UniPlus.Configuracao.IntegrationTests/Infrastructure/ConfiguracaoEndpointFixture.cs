namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

using System.Diagnostics.CodeAnalysis;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// Fixture de coleção que provisiona Postgres efêmero e sobe a API UniPlus com
/// Wolverine habilitado — exercita os endpoints de Configuracao (via query/command
/// bus) no monólito real. Herda toda a infra de <see cref="MonolitoPostgresFixtureBase{TFactory}"/>.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit ICollectionFixture<T> requires the fixture type to be public.")]
public sealed class ConfiguracaoEndpointFixture : MonolitoPostgresFixtureBase<MonolitoApiFactory>
{
    protected override MonolitoApiFactory CreateFactory(string connectionString) => new ConfiguracaoEndpointFactory(connectionString);
}
