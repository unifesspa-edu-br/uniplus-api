namespace Unifesspa.UniPlus.Selecao.API;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Hateoas;
using Unifesspa.UniPlus.Infrastructure.Core.Routing;
using Unifesspa.UniPlus.Selecao.API.Errors;
using Unifesspa.UniPlus.Selecao.API.Hateoas;
using Unifesspa.UniPlus.Selecao.API.Rotinas;
using Unifesspa.UniPlus.Selecao.Application.Commands.DocumentosDaInscricao;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Mappings;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Application.Queries.AtosDoProcesso;
using Unifesspa.UniPlus.Selecao.Infrastructure;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// Registro self-describing do módulo Selecao para o composition root do
/// monólito modular. Reúne tudo que é específico do módulo — OpenAPI
/// doc, registro de erros de domínio, builders HATEOAS, idempotência sobre o
/// DbContext do módulo, Application + Infrastructure e migrations on startup.
///
/// O que é compartilhado entre módulos (Serilog, auth, CORS, cache, storage,
/// observabilidade, middleware, AddControllers, AddDomainErrorMapper, health
/// checks de infra) fica no host. O wiring de Wolverine (ADR-0003/0004/0005) —
/// discovery de assembly e composição de routing — é setup de host/processo e
/// permanece no Program.cs. Mantém o módulo extraível: um serviço próprio
/// chamaria este mesmo método.
/// </summary>
public static class SelecaoModuleRegistration
{
    public static IServiceCollection AddSelecaoModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        RegistrarContratoHttp(services, configuration);
        services.AddIdempotency<SelecaoDbContext, SelecaoApiAssemblyMarker>(configuration);
        RegistrarPermissoesEConferenciaDoDto(services);
        RegistrarAplicacaoEPersistencia(services);
        RegistrarRemocaoDeArquivosPendentes(services, configuration);
        RegistrarReconciliacaoDaVerificacao(services, configuration);
        RegistrarAtosDoProcesso(services, configuration);

        return services;
    }

    /// <summary>
    /// OpenAPI 3.1 (ADR-0030): documento nomeado por módulo, com os transformers Uni+, exposto em
    /// <c>/openapi/selecao.json</c>. Os erros de domínio do módulo são consumidos pelo <c>AddDomainErrorMapper</c>,
    /// registrado uma vez no host via <c>IEnumerable&lt;IDomainErrorRegistration&gt;</c>. O HATEOAS Level 1 (ADR-0029)
    /// tem um builder de <c>_links</c> por recurso, singleton porque encapsula só um <c>LinkGenerator</c> (também
    /// singleton) e é função pura.
    /// </summary>
    private static void RegistrarContratoHttp(IServiceCollection services, IConfiguration configuration)
    {
        services.AddUniPlusOpenApi(SelecaoApiAssemblyMarker.ModuleName, configuration);
        services.AddSingleton<IDomainErrorRegistration, SelecaoDomainErrorRegistration>();
        services.AddSingleton<IResourceLinksBuilder<ObrigatoriedadeLegalDto>, ObrigatoriedadeLegalLinksBuilder>();
        services.AddSingleton<IResourceLinksBuilder<MotivoDeDecisaoDto>, MotivoDeDecisaoLinksBuilder>();
        services.AddSingleton<IResourceLinksBuilder<ProcessoSeletivoDto>, ProcessoSeletivoLinksBuilder>();
        services.AddSingleton<IResourceLinksBuilder<RegraCatalogoDto>, RegraCatalogoLinksBuilder>();
    }

    /// <summary>
    /// O enum de permissões do módulo entra no catálogo da aplicação e é conferido na subida (ADR-0151). O DTO
    /// administrativo declara a classificação de cada propriedade lida da coluna, e a subida confere que nenhuma fica
    /// abaixo da classificação do modelo (ADR-0153).
    /// </summary>
    private static void RegistrarPermissoesEConferenciaDoDto(IServiceCollection services)
    {
        services.AddCatalogoDePermissoes<PermissaoSelecao>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<Microsoft.Extensions.Hosting.IHostedService, ConferenciaDoDtoAdministrativoNaSubida>());
    }

    /// <summary>
    /// O <c>AddSelecaoInfrastructure</c> resolve a connection string pelo <c>IConfiguration</c> injetado no factory do
    /// <c>AddDbContext</c>, simétrico ao <c>UseWolverineOutboxCascading</c>, que também lê tarde: test hosts podem
    /// sobrescrever por env var ou <c>InMemoryCollection</c> sem re-registrar o DbContext. As migrations EF Core do
    /// módulo rodam no <c>StartAsync</c> do host por <c>IHostedService</c>, filtrável pelas test factories que sobem o
    /// pipeline HTTP sem Postgres real (ver <c>ApiFactoryBase</c>).
    /// </summary>
    /// <remarks>
    /// Invariante: este registro precede <c>UseWolverineOutboxCascading</c> e <c>AddWolverineMessaging</c> no
    /// Program.cs. Com <c>HostOptions.ServicesStartConcurrently = false</c> (padrão), os hosted services iniciam em
    /// sequência na ordem de registro, e o <c>MigrationHostedService</c> aplica o schema EF do domínio ANTES de o
    /// <c>WolverineRuntime</c> aceitar o primeiro envelope cascading, evitando 42P01 em handlers que tocam tabelas do
    /// módulo. O <c>MigrationBeforeWolverineRuntimeOrderTests</c> trava a ordem nos três entry points.
    /// </remarks>
    private static void RegistrarAplicacaoEPersistencia(IServiceCollection services)
    {
        services.AddSelecaoApplication();
        services.AddSelecaoInfrastructure();
        services.AddDbContextMigrationsOnStartup<SelecaoDbContext>();
    }

    /// <summary>
    /// Rotina periódica que remove os envios de arquivo pendentes vencidos. Registrada depois das migrations e com a
    /// primeira execução um intervalo após a partida; os hosts de teste a removem pelo tipo base (<c>ApiFactoryBase</c>).
    /// A seção é opcional: sem ela vale o padrão.
    /// </summary>
    private static void RegistrarRemocaoDeArquivosPendentes(IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddOptions<RemocaoDeArquivosPendentesOptions>()
            .Bind(configuration.GetSection(RemocaoDeArquivosPendentesOptions.SectionName))
            .Validate(
                static options => options.Intervalo >= RemocaoDeArquivosPendentesOptions.IntervaloMinimo
                    && options.Intervalo <= RemocaoDeArquivosPendentesOptions.IntervaloMaximo,
                $"{RemocaoDeArquivosPendentesOptions.SectionName}:Intervalo deve ficar entre {RemocaoDeArquivosPendentesOptions.IntervaloMinimo} e {RemocaoDeArquivosPendentesOptions.IntervaloMaximo}.")
            .ValidateOnStart();
        services.AddHostedService<RemocaoDeArquivosPendentesVencidosHostedService>();
    }

    private static void RegistrarReconciliacaoDaVerificacao(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ReconciliacaoDaVerificacaoOptions>()
            .Bind(configuration.GetSection(ReconciliacaoDaVerificacaoOptions.SectionName))
            .Validate(
                static options => options.Prazo >= ReconciliacaoDaVerificacaoOptions.PrazoMinimo
                    && options.PrazoMaximo >= options.Prazo
                    && options.Intervalo >= ReconciliacaoDaVerificacaoOptions.IntervaloMinimo
                    && options.TamanhoDoLote > 0,
                $"{ReconciliacaoDaVerificacaoOptions.SectionName}: Prazo de pelo menos {ReconciliacaoDaVerificacaoOptions.PrazoMinimo} e PrazoMaximo não menor que ele, Intervalo de pelo menos {ReconciliacaoDaVerificacaoOptions.IntervaloMinimo} e TamanhoDoLote positivo.")
            .ValidateOnStart();
        services.AddHostedService<ReconciliacaoDaVerificacaoHostedService>();
    }

    private static void RegistrarAtosDoProcesso(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AtosDoProcessoOptions>()
            .Bind(configuration.GetSection(AtosDoProcessoOptions.SectionName))
            .Validate(
                static options => options.LimiarDePendencia >= AtosDoProcessoOptions.LimiarMinimo,
                $"{AtosDoProcessoOptions.SectionName}:LimiarDePendencia deve ser de pelo menos {AtosDoProcessoOptions.LimiarMinimo}.")
            .ValidateOnStart();
    }
}
