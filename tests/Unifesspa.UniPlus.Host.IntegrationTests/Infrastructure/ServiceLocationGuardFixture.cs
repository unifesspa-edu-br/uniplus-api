namespace Unifesspa.UniPlus.Host.IntegrationTests.Infrastructure;

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
/// A geração de código constrói a classe gerada e pré-resolve os singletons.
/// Com a porta fechada e <c>abortConnect=false</c>, a conexão devolve um
/// multiplexador desconectado sem lançar exceção. Nenhum handler é executado.
/// </remarks>
public sealed class ServiceLocationGuardApiFactory : MonolitoApiFactory
{
    public ServiceLocationGuardApiFactory(string connectionString)
        : base(connectionString, wolverineEnabled: true)
    {
    }

    protected override IEnumerable<KeyValuePair<string, string?>> OverridesAdicionais() =>
    [
        // A geração pré-resolve o singleton; a porta fechada devolve um multiplexador
        // desconectado sem impedir a verificação do grafo.
        new("Redis:ConnectionString", "localhost:6399,abortConnect=false,connectTimeout=200"),

        // A geração também pré-resolve o cliente do armazenamento; o endereço é fictício e nunca é acessado.
        new("Storage:Endpoint", "armazenamento-de-teste:9000"),
        new("Storage:AccessKey", "teste"),
        new("Storage:SecretKey", "teste"),
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
public sealed class ColecaoDaGuardaDeServiceLocation : ICollectionFixture<ServiceLocationGuardFixture>
{
    public const string Name = "Service Location Guard";
}
