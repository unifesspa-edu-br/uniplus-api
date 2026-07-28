namespace Unifesspa.UniPlus.Discentes.API;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Discentes.Infrastructure;
using Unifesspa.UniPlus.Discentes.Infrastructure.Persistence;
using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;

/// <summary>
/// Registro self-describing do módulo Discentes para o composition root do
/// monólito modular. Nesta fase (réplica de dados, sem endpoints públicos) só
/// registra Infrastructure e as migrations on startup — OpenAPI, HATEOAS,
/// Idempotency-Key e Application entram quando o módulo ganhar endpoints/handlers
/// (tasks seguintes de sincronização SIGAA).
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "Referenciado pelo composition root (host do monólito modular) fora deste assembly.")]
public static class DiscentesModuleRegistration
{
    public static IServiceCollection AddDiscentesModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddDiscentesInfrastructure();

        // Migrations EF Core aplicadas no host StartAsync via IHostedService, ANTES
        // do runtime Wolverine (invariante #419 — MigrationBeforeWolverineRuntimeOrder).
        services.AddDbContextMigrationsOnStartup<DiscentesDbContext>();

        return services;
    }
}
