namespace Unifesspa.UniPlus.Portal.Infrastructure;

using Microsoft.Extensions.DependencyInjection;

using Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Persistence;

public static class PortalInfrastructureRegistration
{
    private const string ConnectionStringName = "PortalDb";

    /// <summary>
    /// Registra a infraestrutura do módulo Portal (DbContext + interceptors).
    /// Wire-up centralizado em <see cref="UniPlusDbContextOptionsExtensions"/>
    /// (ADR-0050).
    /// </summary>
    public static IServiceCollection AddPortalInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddUniPlusEfInterceptors();

        services.AddDbContext<PortalDbContext>((serviceProvider, options) =>
            options.UseUniPlusNpgsqlConventions<PortalDbContext>(serviceProvider, ConnectionStringName));

        services.AddScoped<IUnitOfWork>(serviceProvider =>
            serviceProvider.GetRequiredService<PortalDbContext>());

        return services;
    }
}
