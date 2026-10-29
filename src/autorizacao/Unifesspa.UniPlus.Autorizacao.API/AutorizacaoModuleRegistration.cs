namespace Unifesspa.UniPlus.Autorizacao.API;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Autorizacao.API.Errors;
using Unifesspa.UniPlus.Autorizacao.Application;
using Unifesspa.UniPlus.Autorizacao.Infrastructure;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Routing;

/// <summary>
/// Registro self-describing do módulo Autorização para o composition root do monólito
/// modular: OpenAPI doc, erros de domínio, idempotência sobre o DbContext do módulo,
/// Application + Infrastructure e migrations na subida. O que é compartilhado entre módulos
/// (auth, CORS, cache, observabilidade, mapper de erros) e o wiring do Wolverine ficam no host.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "Referenciado pelo composition root (host do monólito modular) fora deste assembly.")]
public static class AutorizacaoModuleRegistration
{
    public static IServiceCollection AddAutorizacaoModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // OpenAPI 3.1 (ADR-0030) — documento nomeado por módulo. Spec em /openapi/autorizacao.json.
        services.AddUniPlusOpenApi(AutorizacaoApiAssemblyMarker.ModuleName, configuration);

        services.AddSingleton<IDomainErrorRegistration, AutorizacaoDomainErrorRegistration>();

        // Idempotency-Key (ADR-0027) sobre o DbContext do módulo.
        services.AddIdempotency<AutorizacaoDbContext, AutorizacaoApiAssemblyMarker>(configuration);

        services.AddAutorizacaoApplication();
        services.AddAutorizacaoInfrastructure();

        // Migrations aplicadas no StartAsync, ANTES do runtime Wolverine (invariante de ordem).
        services.AddDbContextMigrationsOnStartup<AutorizacaoDbContext>();

        return services;
    }
}
