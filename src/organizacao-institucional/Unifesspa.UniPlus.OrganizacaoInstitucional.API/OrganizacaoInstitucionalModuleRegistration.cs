namespace Unifesspa.UniPlus.OrganizacaoInstitucional.API;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Hateoas;
using Unifesspa.UniPlus.OrganizacaoInstitucional.API.Errors;
using Unifesspa.UniPlus.OrganizacaoInstitucional.API.Hateoas;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.DTOs;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence;

/// <summary>
/// Registro self-describing do módulo OrganizacaoInstitucional para o composition
/// root do monólito modular (spike). Reúne tudo que é específico do módulo —
/// OpenAPI doc, registro de erros de domínio, builders HATEOAS, idempotência sobre
/// o DbContext do módulo, Application + Infrastructure e migrations on startup.
///
/// O que é compartilhado entre módulos (Serilog, auth, CORS, cache, storage,
/// observabilidade, middleware, AddControllers, AddDomainErrorMapper, criptografia,
/// cursor pagination, health checks de infra) fica no host. O wiring do Wolverine
/// (consolidado numa única instância) também é responsabilidade do host. Mantém o
/// módulo extraível: um serviço próprio chamaria este mesmo método.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "Referenciado pelo composition root (host do monólito modular) fora deste assembly.")]
public static class OrganizacaoInstitucionalModuleRegistration
{
    public static IServiceCollection AddOrganizacaoInstitucionalModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // OpenAPI 3.1 (ADR-0030) — documento nomeado por módulo. Spec em /openapi/organizacao.json.
        services.AddUniPlusOpenApi("organizacao", configuration);

        // Erros de domínio do módulo (consumidos por AddDomainErrorMapper, registrado
        // uma vez no host via IEnumerable<IDomainErrorRegistration>).
        services.AddSingleton<IDomainErrorRegistration, OrganizacaoDomainErrorRegistration>();

        // HATEOAS Level 1 (ADR-0029/0049) — builders de _links.
        services.AddSingleton<IResourceLinksBuilder<UnidadeDto>, UnidadeLinksBuilder>();
        services.AddSingleton<IResourceLinksBuilder<InstituicaoDto>, InstituicaoLinksBuilder>();

        // Idempotency-Key (ADR-0027) sobre o DbContext do módulo. O filter de MVC
        // ativa-se em endpoints com [RequiresIdempotencyKey], persistindo entries
        // cifradas at-rest.
        services.AddIdempotency<OrganizacaoInstitucionalDbContext>(configuration);

        // Application + Infrastructure — registram validators, DbContext, repositórios,
        // readers cross-módulo (ADR-0056) e cache invalidators.
        services.AddOrganizacaoInstitucionalApplication();
        services.AddOrganizacaoInstitucionalInfrastructure();

        // Migrations EF Core aplicadas no host StartAsync via IHostedService, ANTES
        // do runtime Wolverine (invariante #419 — MigrationBeforeWolverineRuntimeOrder).
        services.AddDbContextMigrationsOnStartup<OrganizacaoInstitucionalDbContext>();

        return services;
    }
}
