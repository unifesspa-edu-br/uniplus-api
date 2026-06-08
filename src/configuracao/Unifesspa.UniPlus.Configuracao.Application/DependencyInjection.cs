namespace Unifesspa.UniPlus.Configuracao.Application;

using FluentValidation;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registra recursos da camada Application do módulo Configuracao. Em V1
/// só varre assembly por validators FluentValidation (vazio neste scaffold —
/// commands/queries entram em F2 com as entidades).
/// </summary>
public static class ConfiguracaoApplicationServiceRegistration
{
    public static IServiceCollection AddConfiguracaoApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        System.Reflection.Assembly assembly = typeof(ConfiguracaoApplicationServiceRegistration).Assembly;
        services.AddValidatorsFromAssembly(assembly);

        return services;
    }
}
