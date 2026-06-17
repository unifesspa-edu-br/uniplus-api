namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Interceptors;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// Sobe o Program.cs produtivo do módulo Configuracao apontando para o Postgres
/// efêmero da fixture, com Wolverine habilitado. Re-registra o DbContext sem
/// <c>EnableRetryOnFailure</c> (incompatível com as transações explícitas do
/// Wolverine).
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "WebApplicationFactory<T> derivative used as collection fixture state.")]
public sealed class ConfiguracaoEndpointApiFactory : ApiFactoryBase<Program>
{
    private readonly string _connectionString;

    public ConfiguracaoEndpointApiFactory(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
    }

    protected override bool DisableWolverineRuntimeForTests => false;

    protected override IEnumerable<KeyValuePair<string, string?>> GetConfigurationOverrides() =>
    [
        new("ConnectionStrings:ConfiguracaoDb", _connectionString),
        new("Auth:Authority", "http://localhost/test-realm"),
        new("Auth:Audience", "uniplus"),
    ];

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<ConfiguracaoDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<ConfiguracaoDbContext>();
            services.RemoveAll<IDbContextOptionsConfiguration<ConfiguracaoDbContext>>();
            RemoveAllOptionsConfigurations<DbContextOptions<ConfiguracaoDbContext>>(services);

            services.AddDbContext<ConfiguracaoDbContext>((sp, opts) =>
            {
                opts.UseNpgsql(_connectionString);
                opts.UseSnakeCaseNamingConvention();
                opts.AddInterceptors(
                    sp.GetRequiredService<SoftDeleteInterceptor>(),
                    sp.GetRequiredService<AuditableInterceptor>());
            });
        });
    }

    private static void RemoveAllOptionsConfigurations<TOptions>(IServiceCollection services)
        where TOptions : class
    {
        Type[] configurationTypes =
        [
            typeof(IConfigureOptions<TOptions>),
            typeof(IPostConfigureOptions<TOptions>),
            typeof(IConfigureNamedOptions<TOptions>),
            typeof(IOptionsChangeTokenSource<TOptions>),
        ];

        foreach (Type type in configurationTypes)
        {
            for (int i = services.Count - 1; i >= 0; i--)
            {
                if (services[i].ServiceType == type)
                {
                    services.RemoveAt(i);
                }
            }
        }
    }
}
