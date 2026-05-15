namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Abstractions;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Interfaces;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Caching;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Readers;

/// <summary>
/// Registra a infraestrutura do módulo OrganizacaoInstitucional (DbContext +
/// interceptors + repositório + reader cross-módulo + cache invalidator).
/// Wire-up centralizado em <see cref="UniPlusDbContextOptionsExtensions"/>
/// (ADR-0054).
/// </summary>
public static class OrganizacaoInstitucionalInfrastructureRegistration
{
    private const string ConnectionStringName = "OrganizacaoDb";

    public static IServiceCollection AddOrganizacaoInstitucionalInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddUniPlusEfInterceptors();

        services.AddDbContext<OrganizacaoInstitucionalDbContext>((serviceProvider, options) =>
            options.UseUniPlusNpgsqlConventions<OrganizacaoInstitucionalDbContext>(serviceProvider, ConnectionStringName));

        // IUnitOfWork roteia para o DbContext do módulo — Application consome via abstração (ADR-0042).
        services.AddScoped<IUnitOfWork>(serviceProvider =>
            serviceProvider.GetRequiredService<OrganizacaoInstitucionalDbContext>());

        services.AddScoped<IAreaOrganizacionalRepository, AreaOrganizacionalRepository>();

        // Reader cross-módulo (ADR-0055/0056) + cache invalidator. Ambos Scoped:
        // dependem de ICacheService (Scoped) e DbContext (Scoped). Singleton causaria
        // captive dependency e exceção em Development com ValidateScopes=true.
        services.AddScoped<IAreaOrganizacionalReader, AreaOrganizacionalReader>();
        services.AddScoped<IAreaOrganizacionalCacheInvalidator, AreaOrganizacionalCacheInvalidator>();

        return services;
    }
}
