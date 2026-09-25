namespace Unifesspa.UniPlus.Portal.Application;

using FluentValidation;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registra recursos da camada Application da Portal API. Os handlers são
/// descobertos pelo Wolverine (convention-based) a partir deste assembly, incluído
/// explicitamente no <c>Discovery</c> do composition root da Portal.
/// </summary>
public static class PortalApplicationServiceRegistration
{
    public static IServiceCollection AddPortalApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        System.Reflection.Assembly assembly = typeof(PortalApplicationAssemblyMarker).Assembly;
        services.AddValidatorsFromAssembly(assembly);

        return services;
    }
}
