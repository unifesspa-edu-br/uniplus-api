namespace Unifesspa.UniPlus.Publicacoes.API;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Publicacoes.API.Errors;
using Unifesspa.UniPlus.Publicacoes.Infrastructure;
using Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence;

/// <summary>
/// Registro self-describing do módulo Publicações para o composition root do
/// monólito modular (ADR-0097). Reúne o que é específico do módulo — OpenAPI
/// doc, registro de erros de domínio, Infrastructure e migrations on startup.
///
/// Publicações nasce como módulo esqueleto: schema próprio, sem entidades e sem
/// endpoints. O cadastro de tipos de ato, o ato normativo e o vínculo
/// ato↔entidade chegam nas stories seguintes.
///
/// O módulo não referencia Domain, Application, Infrastructure ou API de nenhum
/// outro módulo (ADR-0105): ele possui a essência documental do ato, e nada mais.
/// Como não há handlers, o assembly não entra no discovery do Wolverine — a
/// primeira story que introduzir um handler deve compor o discovery no host.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "Referenciado pelo composition root (host do monólito modular) fora deste assembly.")]
public static class PublicacoesModuleRegistration
{
    public static IServiceCollection AddPublicacoesModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // OpenAPI 3.1 (ADR-0030) — spec em /openapi/publicacoes.json.
        services.AddUniPlusOpenApi("publicacoes", configuration);

        // Erros de domínio do módulo (consumidos por AddDomainErrorMapper, registrado
        // uma vez no host via IEnumerable<IDomainErrorRegistration>).
        services.AddSingleton<IDomainErrorRegistration, PublicacoesDomainErrorRegistration>();

        services.AddPublicacoesInfrastructure();

        // Migrations EF Core aplicadas no host StartAsync via IHostedService, ANTES
        // do runtime Wolverine (invariante #419 — MigrationBeforeWolverineRuntimeOrder).
        services.AddDbContextMigrationsOnStartup<PublicacoesDbContext>();

        return services;
    }
}
