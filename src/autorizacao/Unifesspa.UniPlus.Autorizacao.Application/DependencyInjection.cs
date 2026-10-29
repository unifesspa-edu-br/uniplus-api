namespace Unifesspa.UniPlus.Autorizacao.Application;

using FluentValidation;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registra a camada Application do módulo Autorização. O CQRS roda sobre Wolverine, então
/// só os validators FluentValidation, consumidos pelo middleware do Wolverine, são registrados.
/// </summary>
public static class AutorizacaoApplicationServiceRegistration
{
    public static IServiceCollection AddAutorizacaoApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddValidatorsFromAssembly(typeof(AutorizacaoApplicationServiceRegistration).Assembly);

        return services;
    }
}
