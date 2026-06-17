namespace Unifesspa.UniPlus.Geo.Application;

using FluentValidation;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registra recursos da camada Application do módulo Geo. Em V1 só varre o
/// assembly por validators FluentValidation (vazio neste scaffold —
/// commands/queries entram nas Stories de API).
/// </summary>
public static class GeoApplicationServiceRegistration
{
    public static IServiceCollection AddGeoApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        System.Reflection.Assembly assembly = typeof(GeoApplicationServiceRegistration).Assembly;
        services.AddValidatorsFromAssembly(assembly);

        return services;
    }
}
