namespace Unifesspa.UniPlus.Selecao.API;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Hateoas;
using Unifesspa.UniPlus.Selecao.API.Errors;
using Unifesspa.UniPlus.Selecao.API.Hateoas;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Mappings;
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
/// checks de infra) fica no host. O wiring de mensageria do módulo —
/// Kafka/Schema Registry (ADR-0051) e Wolverine (ADR-0003/0004/0005), incluindo
/// o routing cascading e a criação do ISchemaRegistryClient — é setup de
/// host/processo e permanece no Program.cs (será consolidado no host fora deste
/// escopo). Mantém o módulo extraível: um serviço próprio chamaria este mesmo
/// método.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "Referenciado pelo composition root (host do monólito modular) fora deste assembly.")]
public static class SelecaoModuleRegistration
{
    public static IServiceCollection AddSelecaoModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // OpenAPI 3.1 (ADR-0030) — documento nomeado por módulo + transformers
        // Uni+ (info, operation, schema). Spec exposto em /openapi/selecao.json.
        services.AddUniPlusOpenApi("selecao", configuration);

        // Erros de domínio do módulo (consumidos por AddDomainErrorMapper, registrado
        // uma vez no host via IEnumerable<IDomainErrorRegistration>).
        services.AddSingleton<IDomainErrorRegistration, SelecaoDomainErrorRegistration>();

        // HATEOAS Level 1 (ADR-0029) — builder de _links por recurso. Singleton
        // porque encapsula apenas um LinkGenerator (também singleton); função pura.
        services.AddSingleton<IResourceLinksBuilder<EditalDto>, EditalLinksBuilder>();
        services.AddSingleton<IResourceLinksBuilder<ObrigatoriedadeLegalDto>, ObrigatoriedadeLegalLinksBuilder>();
        services.AddSingleton<IResourceLinksBuilder<ProcessoSeletivoDto>, ProcessoSeletivoLinksBuilder>();

        // Idempotency-Key (ADR-0027) — store EF adjacente ao SelecaoDbContext, filter
        // global que se ativa apenas em endpoints com [RequiresIdempotencyKey].
        services.AddIdempotency<SelecaoDbContext, SelecaoApiAssemblyMarker>(configuration);

        services.AddSelecaoApplication();
        // AddSelecaoInfrastructure resolve a connection string via IConfiguration
        // injetada no factory do AddDbContext (issue #204) — simetria com
        // UseWolverineOutboxCascading que já fazia leitura lazy. Test hosts
        // (CascadingApiFactory) podem sobrescrever via env var ou InMemoryCollection
        // sem precisar re-registrar o DbContext.
        services.AddSelecaoInfrastructure();

        // Migrations EF Core do módulo Selecao aplicadas no host StartAsync via
        // IHostedService (issue #344). Como hosted service, o registro é filtrável
        // por test factories que sobem o pipeline HTTP sem Postgres real (ver
        // ApiFactoryBase). Idempotente.
        //
        // INVARIANTE (#419): este registro precede UseWolverineOutboxCascading +
        // AddWolverineMessaging no Program.cs. HostOptions.ServicesStartConcurrently=false
        // (default) garante que IHostedService inicia sequencialmente na ordem de
        // registro — então MigrationHostedService aplica o schema EF do domínio ANTES
        // do WolverineRuntime aceitar o primeiro envelope cascading, evitando 42P01 em
        // handlers que tocam tabelas do módulo. Fitness test em
        // tests/Unifesspa.UniPlus.ArchTests/Hosting/MigrationBeforeWolverineRuntimeOrderTests
        // trava regressão de ordem nos 3 entry points (Selecao/Ingresso/Portal).
        services.AddDbContextMigrationsOnStartup<SelecaoDbContext>();

        return services;
    }
}
