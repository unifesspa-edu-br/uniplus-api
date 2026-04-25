namespace Unifesspa.UniPlus.Selecao.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Interceptors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Infrastructure.ExternalServices;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using Wolverine.EntityFrameworkCore;

public static class SelecaoInfrastructureRegistration
{
    public const string ConnectionStringName = "SelecaoDb";
    public const string WolverineSchema = "wolverine";

    public static IServiceCollection AddSelecaoInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<SoftDeleteInterceptor>();
        services.AddSingleton<AuditableInterceptor>();

        // AddDbContextWithWolverineIntegration anexa os interceptors do Wolverine
        // ao DbContext: SaveChanges passa a auto-publicar domain events de
        // EntityBase.DomainEvents para wolverine.wolverine_outgoing_envelopes
        // dentro da mesma transação (atomicidade write+evento — issue #135).
        services.AddDbContextWithWolverineIntegration<SelecaoDbContext>((serviceProvider, options) =>
        {
            // Resolver lazy via IConfiguration permite que WebApplicationFactory
            // injete overrides (ex.: Testcontainers) antes do DbContext materializar.
            string connectionString = serviceProvider.GetRequiredService<IConfiguration>()
                .GetConnectionString(ConnectionStringName)
                ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' não configurada.");

            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(SelecaoDbContext).Assembly.FullName);
                // SPIKE V3: EnableRetryOnFailure conflita com AutoApplyTransactions
                // do Wolverine — handler abre transação user-initiated e
                // NpgsqlRetryingExecutionStrategy rejeita. Decisão arquitetural:
                // ou (a) desligar retry e usar Wolverine's retry policies, ou
                // (b) wrap manual via IExecutionStrategy em todo handler.
                // npgsqlOptions.EnableRetryOnFailure(...);
            });

            options.AddInterceptors(
                serviceProvider.GetRequiredService<SoftDeleteInterceptor>(),
                serviceProvider.GetRequiredService<AuditableInterceptor>());
        }, WolverineSchema);

        services.AddScoped<IUnitOfWork>(serviceProvider =>
            serviceProvider.GetRequiredService<SelecaoDbContext>());

        services.AddScoped<IEditalRepository, EditalRepository>();
        services.AddScoped<IInscricaoRepository, InscricaoRepository>();
        services.AddScoped<IGovBrAuthService, GovBrAuthService>();

        return services;
    }
}
