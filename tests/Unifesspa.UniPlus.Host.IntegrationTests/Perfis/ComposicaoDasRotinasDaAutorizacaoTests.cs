namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using AwesomeAssertions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using OpenTelemetry;
using OpenTelemetry.Metrics;

using Unifesspa.UniPlus.Autorizacao.API;
using Unifesspa.UniPlus.Host.IntegrationTests.Email;

/// <summary>A ordem de registro é a ordem em que o host inicia as rotinas; não garante que elas não rodem ao mesmo tempo.</summary>
public sealed class ComposicaoDasRotinasDaAutorizacaoTests
{
    [Fact(DisplayName = "As rotinas da Autorização são registradas na ordem: expiração das versões, janelas de migração, amostragem da governança")]
    public void Rotinas_SaoRegistradasNaOrdemDeInicio()
    {
        NomesDasRotinas(Modulo()).Should().Equal(
            "ExpiracaoDasVersoesPendentesHostedService",
            "JanelasDeMigracaoHostedService",
            "AmostragemDaGovernancaHostedService");
    }

    [Fact(DisplayName = "Cada rotina da Autorização é registrada uma só vez")]
    public void Rotinas_SaoRegistradasUmaSoVez()
    {
        NomesDasRotinas(Modulo()).Should().OnlyHaveUniqueItems();
    }

    [Fact(DisplayName = "O provedor de métricas coleta as séries do medidor da governança de perfis")]
    public void Medidor_EConfiguradoNoProvedorDeMetricas()
    {
        using ExportadorDeMetricasEmMemoria exportador = new();
        ServiceCollection servicos = Modulo();
        servicos.AddSingleton(TimeProvider.System);
        servicos.AddOpenTelemetry().WithMetrics(metricas => metricas.AddReader(new BaseExportingMetricReader(exportador)));
        using ServiceProvider provedor = servicos.BuildServiceProvider();

        provedor.GetRequiredService<MeterProvider>().ForceFlush();

        exportador.UltimaExportacao.Should().Contain(m => m.Instrumento == "uniplus.autorizacao.janelas_abertas");
    }

    private static ServiceCollection Modulo()
    {
        ServiceCollection servicos = new();
        servicos.AddAutorizacaoModule(new ConfigurationBuilder().Build());

        return servicos;
    }

    private static string[] NomesDasRotinas(ServiceCollection servicos) =>
    [
        .. servicos
            .Where(d => d.ServiceType == typeof(IHostedService)
                && d.ImplementationType?.Assembly == typeof(AutorizacaoModuleRegistration).Assembly)
            .Select(d => d.ImplementationType!.Name),
    ];
}
