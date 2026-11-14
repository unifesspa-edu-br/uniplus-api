namespace Unifesspa.UniPlus.Publicacoes.Application;

using FluentValidation;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Publicacoes.Application.Commands.AtosNormativos;

/// <summary>
/// Registra recursos da camada Application do módulo Publicações. Os handlers são
/// descobertos pelo Wolverine (convention-based) a partir deste assembly, incluído
/// explicitamente no <c>Discovery</c> do composition root.
/// </summary>
public static class PublicacoesApplicationServiceRegistration
{
    public static IServiceCollection AddPublicacoesApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        System.Reflection.Assembly assembly = typeof(PublicacoesApplicationServiceRegistration).Assembly;
        services.AddValidatorsFromAssembly(assembly);
        services.AddScoped<ReservaDasVagasDoObjeto>();
        services.AddScoped<CadeiaDeRetificacaoDoAto>();
        services.AddScoped<MontagemDoAtoNormativo>();

        return services;
    }
}
