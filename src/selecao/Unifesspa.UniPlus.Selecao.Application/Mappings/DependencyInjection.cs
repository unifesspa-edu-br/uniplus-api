namespace Unifesspa.UniPlus.Selecao.Application.Mappings;

using FluentValidation;

using Microsoft.Extensions.DependencyInjection;

using Services;

/// <summary>
/// Registra os recursos da camada Application do módulo Seleção. CQRS roda
/// integralmente sobre Wolverine (<c>ICommandBus</c>/<c>IQueryBus</c>) — esta
/// extensão registra os validators FluentValidation, consumidos pelo
/// middleware de validação FluentValidation do Wolverine (<c>UseFluentValidation</c>,
/// configurado em <c>Infrastructure.Core</c>), e os serviços que rodam fora do
/// barramento, como a remoção dos envios de arquivo pendentes vencidos.
/// </summary>
public static class SelecaoApplicationServiceRegistration
{
    public static IServiceCollection AddSelecaoApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        System.Reflection.Assembly assembly = typeof(SelecaoApplicationServiceRegistration).Assembly;

        services.AddValidatorsFromAssembly(assembly);

        // Escopo por execução da rotina periódica: os repositórios e o DbContext são do escopo.
        services.AddScoped<RemocaoDeArquivosPendentesVencidos>();

        return services;
    }
}
