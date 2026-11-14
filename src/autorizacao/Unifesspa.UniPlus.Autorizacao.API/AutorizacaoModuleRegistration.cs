namespace Unifesspa.UniPlus.Autorizacao.API;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using OpenTelemetry.Metrics;

using Unifesspa.UniPlus.Autorizacao.API.Errors;
using Unifesspa.UniPlus.Autorizacao.API.Rotinas;
using Unifesspa.UniPlus.Autorizacao.Application;
using Unifesspa.UniPlus.Autorizacao.Application.Monitoramento;
using Unifesspa.UniPlus.Autorizacao.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>
/// Registro self-describing do módulo Autorização para o composition root do monólito
/// modular: OpenAPI doc, erros de domínio, Application e Infrastructure. O que é compartilhado entre módulos
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

        services.AddAutorizacaoApplication();
        services.AddAutorizacaoInfrastructure();
        services.AddHostedService<ExpiracaoDasVersoesPendentesHostedService>();
        services.AddHostedService<JanelasDeMigracaoHostedService>();
        AddMonitoramento(services);

        return services;
    }

    // O medidor nasce antes do provedor de métricas: a série existe desde a primeira coleta.
    private static void AddMonitoramento(IServiceCollection services)
    {
        services.AddHostedService<AmostragemDaGovernancaHostedService>();
        services.ConfigureOpenTelemetryMeterProvider(static (sp, builder) =>
            builder.AddMeter(sp.GetRequiredService<MetricasDaGovernancaDePerfis>().Medidor.Name));
    }
}
