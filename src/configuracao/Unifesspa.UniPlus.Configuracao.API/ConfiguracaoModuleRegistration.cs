namespace Unifesspa.UniPlus.Configuracao.API;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.API.Errors;
using Unifesspa.UniPlus.Configuracao.API.Hateoas;
using Unifesspa.UniPlus.Configuracao.Application;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Infrastructure;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Hateoas;

/// <summary>
/// Registro self-describing do módulo Configuracao para o composition root do
/// monólito modular. Reúne tudo que é específico do módulo — OpenAPI
/// doc, registro de erros de domínio, builders HATEOAS, idempotência sobre o
/// DbContext do módulo, Application + Infrastructure e migrations on startup.
///
/// O que é compartilhado entre módulos (Serilog, auth, CORS, cache, storage,
/// observabilidade, middleware, AddControllers, AddDomainErrorMapper, health
/// checks de infra) fica no host. O wiring do Wolverine (consolidado numa única
/// instância) também é responsabilidade do host. Mantém o módulo extraível:
/// um serviço próprio chamaria este mesmo método.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "Referenciado pelo composition root (host do monólito modular) fora deste assembly.")]
public static class ConfiguracaoModuleRegistration
{
    public static IServiceCollection AddConfiguracaoModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // OpenAPI 3.1 (ADR-0030) — spec em /openapi/configuracao.json.
        services.AddUniPlusOpenApi("configuracao", configuration);

        // Erros de domínio do módulo (consumidos por AddDomainErrorMapper, registrado
        // uma vez no host via IEnumerable<IDomainErrorRegistration>).
        services.AddSingleton<IDomainErrorRegistration, ConfiguracaoDomainErrorRegistration>();

        // HATEOAS Level 1 (ADR-0029) — builders de _links dos cadastros (UNI-REQ #587).
        services.AddSingleton<IResourceLinksBuilder<CampusDto>, CampusLinksBuilder>();
        services.AddSingleton<IResourceLinksBuilder<LocalOfertaDto>, LocalOfertaLinksBuilder>();
        services.AddSingleton<IResourceLinksBuilder<ReferenciaReservaDemograficaDto>, ReferenciaReservaDemograficaLinksBuilder>();
        services.AddSingleton<IResourceLinksBuilder<PesoAreaEnemDto>, PesoAreaEnemLinksBuilder>();
        services.AddSingleton<IResourceLinksBuilder<TipoDocumentoDto>, TipoDocumentoLinksBuilder>();
        services.AddSingleton<IResourceLinksBuilder<CondicaoAtendimentoDto>, CondicaoAtendimentoLinksBuilder>();
        services.AddSingleton<IResourceLinksBuilder<RecursoAcessibilidadeDto>, RecursoAcessibilidadeLinksBuilder>();
        services.AddSingleton<IResourceLinksBuilder<TipoDeficienciaDto>, TipoDeficienciaLinksBuilder>();
        services.AddSingleton<IResourceLinksBuilder<ModalidadeDto>, ModalidadeLinksBuilder>();
        services.AddSingleton<IResourceLinksBuilder<FaseCanonicaDto>, FaseCanonicaLinksBuilder>();
        services.AddSingleton<IResourceLinksBuilder<TipoBancaDto>, TipoBancaLinksBuilder>();

        // Idempotency-Key (ADR-0027) sobre o DbContext do módulo.
        services.AddIdempotency<ConfiguracaoDbContext, ConfiguracaoApiAssemblyMarker>(configuration);

        services.AddConfiguracaoApplication();
        services.AddConfiguracaoInfrastructure();

        // Migrations EF Core aplicadas no host StartAsync via IHostedService, ANTES
        // do runtime Wolverine (invariante #419 — MigrationBeforeWolverineRuntimeOrder).
        services.AddDbContextMigrationsOnStartup<ConfiguracaoDbContext>();

        return services;
    }
}
