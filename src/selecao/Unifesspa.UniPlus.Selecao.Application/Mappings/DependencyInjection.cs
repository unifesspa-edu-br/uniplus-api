namespace Unifesspa.UniPlus.Selecao.Application.Mappings;

using FluentValidation;

using Microsoft.Extensions.DependencyInjection;

using Services;

using Unifesspa.UniPlus.Authorization.Projecao;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;

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
        AddDefinicaoDoCronogramaDeFases(services);

        // Escopo por execução da rotina periódica: os repositórios e o DbContext são do escopo.
        services.AddScoped<RemocaoDeArquivosPendentesVencidos>();

        // A porta lida do catálogo do módulo, para o projetor saber qual permissão atende a cada par.
        services.AddSingleton<IPortaDePermissaoPorPar, PortaDePermissaoPorPar<PermissaoSelecao>>();

        return services;
    }

    // O serviço de definição e a montagem das fases não guardam estado de uma edição para outra: as portas entram
    // por construtor, e o handler recebe os dois prontos.
    private static void AddDefinicaoDoCronogramaDeFases(IServiceCollection services)
    {
        services.AddScoped<MontagemDasFases>();
        services.AddScoped<DefinicaoDoCronogramaDeFases>();
    }
}
