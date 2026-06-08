namespace Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Infrastructure;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Interceptors;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence;

/// <summary>
/// <see cref="Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory{TEntryPoint}"/> que sobe
/// o Program.cs produtivo do módulo OrganizacaoInstitucional apontando para o
/// Postgres efêmero da fixture. Re-registra o DbContext sem
/// <c>EnableRetryOnFailure</c> (incompatível com transações explícitas do
/// Wolverine).
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "WebApplicationFactory<T> derivative used as collection fixture state.")]
public sealed class OrganizacaoEndpointApiFactory : ApiFactoryBase<Program>
{
    private readonly string _connectionString;

    public OrganizacaoEndpointApiFactory(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
    }

    // Esta suite valida o pipeline HTTP completo com Wolverine rodando.
    // A fixture provisiona Postgres efêmero, então o WolverineRuntime
    // não pode ser removido.
    protected override bool DisableWolverineRuntimeForTests => false;

    protected override IEnumerable<KeyValuePair<string, string?>> GetConfigurationOverrides() =>
    [
        new("ConnectionStrings:OrganizacaoDb", _connectionString),
        new("Auth:Authority", "http://localhost/test-realm"),
        new("Auth:Audience", "uniplus"),
    ];

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
        {
            // Remove o registro do DbContext produtivo (com EnableRetryOnFailure)
            // e re-registra apontando para o container efêmero sem retry —
            // o retry é incompatível com transações user-initiated do Wolverine.
            services.RemoveAll<DbContextOptions<OrganizacaoInstitucionalDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<OrganizacaoInstitucionalDbContext>();
            services.RemoveAll<IDbContextOptionsConfiguration<OrganizacaoInstitucionalDbContext>>();
            RemoveAllOptionsConfigurations<DbContextOptions<OrganizacaoInstitucionalDbContext>>(services);

            services.AddDbContext<OrganizacaoInstitucionalDbContext>((sp, opts) =>
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
