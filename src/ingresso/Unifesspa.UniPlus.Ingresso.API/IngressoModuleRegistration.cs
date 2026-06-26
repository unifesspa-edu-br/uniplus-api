namespace Unifesspa.UniPlus.Ingresso.API;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Ingresso.API.Errors;
using Unifesspa.UniPlus.Ingresso.Infrastructure;
using Unifesspa.UniPlus.Ingresso.Infrastructure.Persistence;

/// <summary>
/// Registro self-describing do módulo Ingresso para o composition root do
/// monólito modular (spike). Reúne tudo que é específico do módulo — OpenAPI
/// doc, registro de erros de domínio, Infrastructure e migrations on startup.
///
/// Ingresso é um módulo esqueleto (3 camadas, sem Application separada — os
/// handlers e a registração vivem em Infrastructure), então tem menos
/// registrações que Configuracao: sem builders HATEOAS e sem idempotência.
///
/// O que é compartilhado entre módulos (Serilog, auth, CORS, cache, storage,
/// observabilidade, middleware, AddControllers, AddDomainErrorMapper, health
/// checks de infra) fica no host. O wiring do Wolverine (consolidado numa única
/// instância) também é responsabilidade do host (P4). Mantém o módulo extraível:
/// um serviço próprio chamaria este mesmo método.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "Referenciado pelo composition root (host do monólito modular) fora deste assembly.")]
public static class IngressoModuleRegistration
{
    public static IServiceCollection AddIngressoModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // OpenAPI 3.1 (ADR-0030) — spec em /openapi/ingresso.json.
        services.AddUniPlusOpenApi("ingresso", configuration);

        // Erros de domínio do módulo (consumidos por AddDomainErrorMapper, registrado
        // uma vez no host via IEnumerable<IDomainErrorRegistration>).
        services.AddSingleton<IDomainErrorRegistration, IngressoDomainErrorRegistration>();

        // AddIngressoInfrastructure resolve a connection string via IConfiguration
        // injetada no factory do AddDbContext (issue #204) — simetria com
        // UseWolverineOutboxCascading e com Selecao.
        services.AddIngressoInfrastructure();

        // Migrations EF Core aplicadas no host StartAsync via IHostedService, ANTES
        // do runtime Wolverine (invariante #419 — MigrationBeforeWolverineRuntimeOrder).
        services.AddDbContextMigrationsOnStartup<IngressoDbContext>();

        return services;
    }
}
