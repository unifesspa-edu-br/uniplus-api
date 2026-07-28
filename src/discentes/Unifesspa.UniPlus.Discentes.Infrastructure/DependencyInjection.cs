namespace Unifesspa.UniPlus.Discentes.Infrastructure;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Discentes.Application.Abstractions;
using Unifesspa.UniPlus.Discentes.Domain.Interfaces;
using Unifesspa.UniPlus.Discentes.Infrastructure.Persistence;
using Unifesspa.UniPlus.Discentes.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// Registra a infraestrutura do módulo Discentes (DbContext + interceptors +
/// repositórios). <see cref="IUniPlusEncryptionService"/> já é registrado globalmente
/// pelo Host (<c>AddUniPlusEncryption</c>) — este módulo só o consome via DI no
/// repositório de <c>VinculoDiscente</c> (ADR-0121).
/// </summary>
public static class DiscentesInfrastructureRegistration
{
    private const string ConnectionStringName = "DiscentesDb";

    public static IServiceCollection AddDiscentesInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddUniPlusEfInterceptors();

        services.AddDbContext<DiscentesDbContext>((serviceProvider, options) =>
            options.UseUniPlusNpgsqlConventions<DiscentesDbContext>(serviceProvider, ConnectionStringName, schema: DiscentesDbContext.Schema));

        // IDiscentesUnitOfWork roteia para o DbContext do módulo — nunca um 2º
        // AddScoped<IUoW, DbContext> direto (criaria 2ª instância e quebraria a
        // atomicidade write+evento do outbox, ADR-0004).
        services.AddScoped<IDiscentesUnitOfWork>(serviceProvider =>
            serviceProvider.GetRequiredService<DiscentesDbContext>());

        services.AddScoped<IVinculoDiscenteRepository, VinculoDiscenteRepository>();
        services.AddScoped<ISyncRunRepository, SyncRunRepository>();

        return services;
    }
}
