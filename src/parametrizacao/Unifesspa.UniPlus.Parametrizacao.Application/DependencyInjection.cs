namespace Unifesspa.UniPlus.Parametrizacao.Application;

using FluentValidation;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registra recursos da camada Application do módulo Parametrizacao. Em V1
/// só varre assembly por validators FluentValidation (vazio neste scaffold —
/// commands/queries entram em F2 com as entidades).
/// </summary>
public static class ParametrizacaoApplicationServiceRegistration
{
    public static IServiceCollection AddParametrizacaoApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        System.Reflection.Assembly assembly = typeof(ParametrizacaoApplicationServiceRegistration).Assembly;
        services.AddValidatorsFromAssembly(assembly);

        return services;
    }
}
