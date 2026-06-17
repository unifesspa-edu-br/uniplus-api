namespace Unifesspa.UniPlus.Geo.IntegrationTests.Infrastructure;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Collection que compartilha o <see cref="GeoPostgisFixture"/> entre os testes
/// que precisam do Postgres+PostGIS efêmero. <c>DisableParallelization</c> evita
/// corrida nas env vars process-wide setadas pela fixture (mesmo padrão das
/// demais collections de integração com Testcontainers).
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit ICollectionFixture<T> exige tipo público para a definição da coleção.")]
[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Convenção de nome de coleção xUnit termina com 'Collection'.")]
public sealed class GeoPostgisCollection : ICollectionFixture<GeoPostgisFixture>
{
    public const string Name = "geo-postgis";
}
