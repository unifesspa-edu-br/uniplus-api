namespace Unifesspa.UniPlus.Geo.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Geo.Infrastructure.Persistence;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// Registra a infraestrutura do módulo Geo (DbContext + interceptors
/// transversais soft delete + audit). O <c>GeoDbContext</c> ativa o plugin
/// NetTopologySuite do Npgsql via o hook do <c>UseUniPlusNpgsqlConventions</c>
/// (ADR-0091) — paridade com o design-time factory. Repositórios e readers
/// entram nas Stories de domínio/API.
/// </summary>
public static class GeoInfrastructureRegistration
{
    private const string ConnectionStringName = "GeoDb";

    public static IServiceCollection AddGeoInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddUniPlusEfInterceptors();

        services.AddDbContext<GeoDbContext>((serviceProvider, options) =>
            options.UseUniPlusNpgsqlConventions<GeoDbContext>(
                serviceProvider,
                ConnectionStringName,
                npgsql => npgsql.UseNetTopologySuite()));

        services.AddScoped<IUnitOfWork>(serviceProvider =>
            serviceProvider.GetRequiredService<GeoDbContext>());

        return services;
    }
}
