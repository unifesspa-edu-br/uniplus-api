namespace Unifesspa.UniPlus.Autorizacao.API;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using OpenTelemetry.Metrics;

using Unifesspa.UniPlus.Autorizacao.API.Errors;
using Unifesspa.UniPlus.Autorizacao.API.Rotinas;
using Unifesspa.UniPlus.Autorizacao.Application;
using Unifesspa.UniPlus.Autorizacao.Application.Monitoramento;
using Unifesspa.UniPlus.Autorizacao.Infrastructure;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Catalogo;
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
        services.AddHostedService<ExpiracaoDasVersoesPendentesHostedService>();
        services.AddHostedService<JanelasDeMigracaoHostedService>();
        AddMonitoramento(services);

        // Migrations aplicadas no StartAsync, ANTES do runtime Wolverine (invariante de ordem); a conferência do catálogo
        // roda logo depois delas, onde elas rodam.
        services.AddDbContextMigrationsOnStartup<AutorizacaoDbContext>(ConciliarOCatalogoAsync);

        return services;
    }

    // O medidor nasce antes do provedor de métricas: a série existe desde a primeira coleta.
    private static void AddMonitoramento(IServiceCollection services)
    {
        services.AddHostedService<AmostragemDaGovernancaHostedService>();
        services.ConfigureOpenTelemetryMeterProvider(static (sp, builder) =>
            builder.AddMeter(sp.GetRequiredService<MetricasDaGovernancaDePerfis>().Medidor.Name));
    }

    private static async Task ConciliarOCatalogoAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        AsyncServiceScope escopo = services.CreateAsyncScope();
        await using (escopo.ConfigureAwait(false))
        {
            await escopo.ServiceProvider.GetRequiredService<ConciliacaoDoCatalogo>().ExecutarAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
