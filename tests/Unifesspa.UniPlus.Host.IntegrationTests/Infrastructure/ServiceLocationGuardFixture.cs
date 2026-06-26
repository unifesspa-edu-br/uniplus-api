namespace Unifesspa.UniPlus.Host.IntegrationTests.Infrastructure;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Unifesspa.UniPlus.Infrastructure.Core.Caching;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// Factory do monólito para a guarda de service location (ADR-0098). Diferente da
/// <see cref="MonolitoApiFactory"/> padrão, NÃO troca o <see cref="ICacheService"/>
/// pelo fake: mantém o <see cref="RedisCacheService"/> de PRODUÇÃO (que depende do
/// <c>IConnectionMultiplexer</c> singleton via lambda opaca) para que a guarda
/// exercite o codegen sobre a forma real do grafo de DI — sem mascarar o caminho
/// que os cache invalidators do Organização tomam em produção.
/// </summary>
/// <remarks>
/// O <c>IConnectionMultiplexer</c> é singleton: o Wolverine o pré-resolve na
/// construção da classe gerada, sem service location (validado empiricamente — só
/// lambdas <em>Scoped</em> opacas e concretos não-públicos disparam). A connection
/// string aponta para uma porta fechada: a resolução do multiplexer falha rápido com
/// <c>RedisConnectionException</c> em runtime — ruído que a guarda ignora, pois só se
/// importa com <c>InvalidServiceLocationException</c> (lançada na GERAÇÃO, antes).
/// </remarks>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "WebApplicationFactory<T> derivative usado como estado de fixture.")]
public sealed class ServiceLocationGuardApiFactory : MonolitoApiFactory
{
    public ServiceLocationGuardApiFactory(string connectionString)
        : base(connectionString, wolverineEnabled: true)
    {
    }

    protected override IEnumerable<KeyValuePair<string, string?>> OverridesAdicionais() =>
    [
        // Porta fechada → ConnectionMultiplexer.Connect falha rápido (connection
        // refused). Suficiente para o codegen inspecionar o descriptor; o connect só
        // ocorre em runtime, fora do caminho de geração.
        new("Redis:ConnectionString", "localhost:6399,abortConnect=true,connectTimeout=200"),
    ];

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
        {
            // Desfaz o swap do fake (base) e devolve o RedisCacheService de produção.
            services.RemoveAll<ICacheService>();
            services.AddScoped<ICacheService, RedisCacheService>();
        });
    }
}

/// <summary>
/// Fixture da guarda de service location: provisiona Postgres efêmero e sobe o
/// monólito com o cache de PRODUÇÃO via <see cref="ServiceLocationGuardApiFactory"/>.
/// </summary>
[SuppressMessage(
    "Reliability",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Recursos liberados por IAsyncLifetime.DisposeAsync — herdado da base.")]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit ICollectionFixture<T> exige tipo público.")]
public sealed class ServiceLocationGuardFixture : MonolitoPostgresFixtureBase<ServiceLocationGuardApiFactory>
{
    protected override ServiceLocationGuardApiFactory CreateFactory(string connectionString) =>
        new(connectionString);
}

/// <summary>
/// Coleção xUnit dedicada da guarda de service location — isola o seu Postgres
/// (cache de produção) das demais suítes do host.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit CollectionDefinition exige tipo público.")]
[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Convenção de nome de coleção xUnit termina com 'Collection'.")]
public sealed class ServiceLocationGuardCollection : ICollectionFixture<ServiceLocationGuardFixture>
{
    public const string Name = "Service Location Guard";
}
