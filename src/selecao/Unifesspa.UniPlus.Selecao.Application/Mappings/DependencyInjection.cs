namespace Unifesspa.UniPlus.Selecao.Application.Mappings;

using FluentValidation;

using Microsoft.Extensions.DependencyInjection;

using Services;
using Services.InscricaoAdministrativa;

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

        services.AddValidatorsFromAssembly(typeof(SelecaoApplicationServiceRegistration).Assembly);
        AddDefinicaoDoCronogramaDeFases(services);
        AddCongelamentoDaVersao(services);
        AddAcessoAoCampoEAoDocumentoDaInscricao(services);

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

    // A decisão e a leitura da revelação de um campo e da abertura de um documento, pela consulta administrativa, guardam
    // só as portas por construtor, e cada handler os recebe prontos, sem service location.
    private static void AddAcessoAoCampoEAoDocumentoDaInscricao(IServiceCollection services)
    {
        services.AddScoped<AcessoARevelacaoDoCampo>();
        services.AddScoped<LeituraDoIdentificador>();
        services.AddScoped<ClassificacaoDaExigenciaDoArquivo>();
        services.AddScoped<DecisaoDoAcessoAoDocumento>();
        services.AddScoped<EmissaoDoAcessoAoArquivo>();
    }

    // Os serviços que congelam uma versão (publicação, retificação de ato único e fechamento da sessão) guardam só as
    // portas por construtor, e cada handler os recebe prontos, sem service location.
    private static void AddCongelamentoDaVersao(IServiceCollection services)
    {
        services.AddScoped<ConferenciaDoAtoEDoDocumento>();
        services.AddScoped<LeituraDaBaseDaRetificacao>();
        services.AddScoped<LeituraDasVersoesPublicadas>();
        services.AddScoped<ConferenciaDoCatalogoEDaProtecao>();
        services.AddScoped<RecopiaAoCongelar>();
        services.AddScoped<ResolucaoDosTiposDeAtoUnicos>();
        services.AddScoped<ConferenciaDosAnexosDoEdital>();
        services.AddScoped<ConferenciaDeConformidadeLegal>();
        services.AddScoped<ConferenciaDosMotivosAoCongelar>();
        services.AddScoped<ConferenciaDaConfiguracaoAoCongelar>();
        services.AddScoped<LeituraDoContextoDeContagemDePrazos>();
        services.AddScoped<MontagemDoCongelamento>();
        services.AddScoped<GravacaoDaVersaoCongelada>();
        services.AddScoped<CongelamentoDaVersao>();
    }
}
