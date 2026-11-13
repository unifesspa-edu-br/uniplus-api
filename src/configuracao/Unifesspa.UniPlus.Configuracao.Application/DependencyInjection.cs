namespace Unifesspa.UniPlus.Configuracao.Application;

using FluentValidation;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;

/// <summary>
/// Registra recursos da camada Application do módulo Configuracao: os validators FluentValidation do assembly e os
/// serviços do circuito de aprovação da proteção do catálogo, que os handlers recebem prontos.
/// </summary>
public static class ConfiguracaoApplicationServiceRegistration
{
    public static IServiceCollection AddConfiguracaoApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        System.Reflection.Assembly assembly = typeof(ConfiguracaoApplicationServiceRegistration).Assembly;
        services.AddValidatorsFromAssembly(assembly);

        services.AddScoped<ItensSobAprovacao>();
        services.AddScoped<SolicitacaoDaProtecaoDoCatalogo>();
        services.AddScoped<DecisaoNaProtecaoDoCatalogo>();
        services.AddScoped<ManifestacaoNaProtecaoDoCatalogo>();

        return services;
    }
}
