namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Abstractions;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Interfaces;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Caching;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Readers;

/// <summary>
/// Registra a infraestrutura do módulo OrganizacaoInstitucional (repositórios + reader cross-módulo + cache
/// invalidator). O contexto e a unidade de trabalho vêm da persistência única do monólito.
/// </summary>
public static class OrganizacaoInstitucionalInfrastructureRegistration
{
    public static IServiceCollection AddOrganizacaoInstitucionalInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IUnidadeRepository, UnidadeRepository>();
        services.AddScoped<IInstituicaoRepository, InstituicaoRepository>();

        // Readers cross-módulo (ADR-0056) + cache invalidators. Scoped porque
        // dependem de ICacheService (Scoped) e DbContext (Scoped).
        services.AddScoped<IUnidadeReader, UnidadeReader>();
        services.AddScoped<IUnidadeCacheInvalidator, UnidadeCacheInvalidator>();
        services.AddScoped<IInstituicaoReader, InstituicaoReader>();
        services.AddScoped<IInstituicaoCacheInvalidator, InstituicaoCacheInvalidator>();

        return services;
    }
}
