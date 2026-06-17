namespace Unifesspa.UniPlus.Geo.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Geo.Infrastructure.Persistence;
using Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Etl;
using Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Etl.Bulk;
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

        // ETL DNE (ADR-0092) — serviços de carga de reference data. O gatilho (seed
        // dev / endpoint admin) e a fonte concreta de produção entram na Story #674.
        // Topo da hierarquia (País/Estado/Cidade, #672):
        services.AddScoped<IGeoImportador, GeoImportadorPaisEstadoCidade>();

        // Folhas (Distrito/Bairro/Logradouro + satélites, #673): o orquestrador compõe
        // o upsert de Distrito/Bairro e o COPY em lote dos logradouros.
        services.AddScoped<GeoImportadorDistritoBairro>();
        services.AddScoped<LogradouroCopyImporter>();
        services.AddScoped<IGeoImportadorLocalidades, GeoImportadorLocalidades>();

        return services;
    }
}
