namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application;

using FluentValidation;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registra recursos da camada Application do módulo OrganizacaoInstitucional.
/// CQRS roda integralmente sobre Wolverine (<c>ICommandBus</c>/<c>IQueryBus</c>),
/// então esta extensão registra apenas os validators FluentValidation,
/// consumidos pelo <c>WolverineValidationMiddleware</c> em Infrastructure.Core.
/// </summary>
public static class OrganizacaoInstitucionalApplicationServiceRegistration
{
    public static IServiceCollection AddOrganizacaoInstitucionalApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        System.Reflection.Assembly assembly = typeof(OrganizacaoInstitucionalApplicationServiceRegistration).Assembly;
        services.AddValidatorsFromAssembly(assembly);

        return services;
    }
}
