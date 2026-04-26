namespace Unifesspa.UniPlus.Selecao.Application.Mappings;

using FluentValidation;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registra os recursos da camada Application do módulo Seleção. CQRS roda
/// integralmente sobre Wolverine (<c>ICommandBus</c>/<c>IQueryBus</c>) — esta
/// extensão registra apenas os validators FluentValidation, consumidos pelo
/// <c>WolverineValidationMiddleware</c> em <c>Infrastructure.Core</c>.
/// </summary>
public static class SelecaoApplicationServiceRegistration
{
    public static IServiceCollection AddSelecaoApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        System.Reflection.Assembly assembly = typeof(SelecaoApplicationServiceRegistration).Assembly;

        services.AddValidatorsFromAssembly(assembly);

        return services;
    }
}
