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
public static class AutorizacaoModuleRegistration
{
    public static IServiceCollection AddAutorizacaoModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        AddContratoPublicado(services, configuration);

        services.AddAutorizacaoApplication();
        services.AddAutorizacaoInfrastructure();
        AddRotinas(services);
        AddMedidorDaGovernanca(services);

        return services;
    }

    // O que o módulo publica para fora: o documento OpenAPI 3.1 (ADR-0030), com spec em /openapi/autorizacao.json, e o
    // mapeamento dos erros de domínio. A conferência dos códigos dos enums é do executável que compõe o módulo.
    private static void AddContratoPublicado(IServiceCollection services, IConfiguration configuration)
    {
        services.AddUniPlusOpenApi(AutorizacaoApiAssemblyMarker.ModuleName, configuration);
        services.AddSingleton<IDomainErrorRegistration, AutorizacaoDomainErrorRegistration>();
    }

    private static void AddRotinas(IServiceCollection services)
    {
        services.AddHostedService<ExpiracaoDasVersoesPendentesHostedService>();
        services.AddHostedService<RevisoesDasConcessoesHostedService>();
        services.AddHostedService<AmostragemDaGovernancaHostedService>();
    }

    // O medidor nasce antes do provedor de métricas: a série existe desde a primeira coleta.
    private static void AddMedidorDaGovernanca(IServiceCollection services)
    {
        services.ConfigureOpenTelemetryMeterProvider(static (sp, builder) =>
            builder.AddMeter(sp.GetRequiredService<MetricasDaGovernancaDePerfis>().Medidor.Name));
    }
}
