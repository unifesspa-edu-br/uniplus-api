namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Unifesspa.UniPlus.Autorizacao.API;
using Unifesspa.UniPlus.Autorizacao.Application.Monitoramento;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.Infrastructure.Core.Hosting;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Observability;

/// <summary>
/// A rotina que amostra a governança de perfis, que o host de teste remove: aqui ela é montada a partir do registro do
/// módulo e disparada pelo relógio do teste, e a passada entrega o retrato às métricas do host.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class RotinaDaAmostragemDaGovernancaNoHostTests
{
    private readonly MonolitoDaInstalacaoFixture _fixture;

    public RotinaDaAmostragemDaGovernancaNoHostTests(MonolitoDaInstalacaoFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "A passada da rotina de amostragem publica o retrato nos gauges e zera a idade da amostra")]
    public async Task Passada_PublicaOsGaugesEZeraAIdade()
    {
        IServiceProvider servicos = _fixture.Factory.Services;
        MetricasDaGovernancaDePerfis metricas = servicos.GetRequiredService<MetricasDaGovernancaDePerfis>();
        using ColetorDeMedidas coletor = new(metricas.Medidor);
        RelogioComTemporizadorManual relogio = new();
        TaskCompletionSource passada = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using PeriodicJobHostedService rotina = Montar(servicos, relogio, passada);

        await rotina.StartAsync(CancellationToken.None);
        await relogio.DispararAsync();
        await passada.Task;
        await rotina.StopAsync(CancellationToken.None);

        coletor.Ultima("uniplus.autorizacao.amostra.idade").Should().BeLessThan(30);
        coletor.Coletar("uniplus.autorizacao.janelas_abertas").Should().NotBeEmpty();
    }

    private static PeriodicJobHostedService Montar(IServiceProvider servicos, TimeProvider relogio, TaskCompletionSource passada)
    {
        ServiceCollection modulo = new();
        modulo.AddAutorizacaoModule(new ConfigurationBuilder().Build());
        Type tipo = modulo
            .Where(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType?.Name == "AmostragemDaGovernancaHostedService")
            .Select(d => d.ImplementationType!)
            .Single();

        return (PeriodicJobHostedService)ActivatorUtilities.CreateInstance(
            servicos, tipo, new EscopoQueAvisaOFim(servicos.GetRequiredService<IServiceScopeFactory>(), passada), relogio);
    }
}
