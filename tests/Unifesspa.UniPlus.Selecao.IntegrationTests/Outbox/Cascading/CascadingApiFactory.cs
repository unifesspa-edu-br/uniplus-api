namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

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
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// <see cref="WebApplicationFactory{TEntryPoint}"/> que sobe o Program.cs
/// produtivo apontando para o Postgres efêmero da fixture. Re-registra o
/// <see cref="SelecaoDbContext"/> sem <c>EnableRetryOnFailure</c> (incompatível
/// com user-initiated transactions) e adiciona o <see cref="DomainEventCollector"/>
/// que o handler subscritor consome.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "WebApplicationFactory<T> derivative used as collection fixture state.")]
public sealed class CascadingApiFactory : ApiFactoryBase<Program>
{
    private readonly string _connectionString;

    public CascadingApiFactory(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
    }

    // Esta suite valida a infra produtiva de outbox (PG queue, durable envelopes,
    // cascading drainage). A fixture provisiona o Postgres efêmero, então
    // o WolverineRuntime precisa rodar (não pode ser removido pelo default
    // de ApiFactoryBase).
    protected override bool DisableWolverineRuntimeForTests => false;

    protected override IEnumerable<KeyValuePair<string, string?>> GetConfigurationOverrides() =>
    [
        new("ConnectionStrings:SelecaoDb", _connectionString),
        new("Auth:Authority", "http://localhost/test-realm"),
        new("Auth:Audience", "uniplus"),
    ];

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<SelecaoDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<SelecaoDbContext>();
            services.RemoveAll<IDbContextOptionsConfiguration<SelecaoDbContext>>();
            RemoveAllOptionsConfigurations<DbContextOptions<SelecaoDbContext>>(services);

            // Re-registro do DbContext mantém SoftDeleteInterceptor + AuditableInterceptor
            // + UseSnakeCaseNamingConvention simétrico à produção (`SelecaoInfrastructureRegistration.
            // AddSelecaoInfrastructure` via `UseUniPlusNpgsqlConventions`). Sem isto:
            // (a) os interceptors ficariam de fora — as colunas is_deleted/deleted_at/updated_at
            //     não seriam preenchidas automaticamente, mascarando regressões;
            // (b) sem snake_case, o model em runtime ficaria em PascalCase implícito enquanto
            //     o Snapshot regenerado pelo `dotnet ef` está em snake_case (ADR-0054), e o EF
            //     dispararia PendingModelChangesWarning bloqueando MigrateAsync.
            services.AddDbContext<SelecaoDbContext>((sp, opts) =>
            {
                opts.UseNpgsql(_connectionString);
                opts.UseSnakeCaseNamingConvention();
                opts.AddInterceptors(
                    sp.GetRequiredService<SoftDeleteInterceptor>(),
                    sp.GetRequiredService<AuditableInterceptor>());
            });

            services.AddSingleton<DomainEventCollector>();
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
