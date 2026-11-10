namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Unifesspa.UniPlus.Autorizacao.API;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.Infrastructure.Core.Hosting;

/// <summary>
/// A rotina periódica do módulo, que o host de teste remove: aqui ela é montada a partir do registro do módulo e
/// disparada pelo relógio do teste, e a mesma passada processa os avisos da reclassificação.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class RotinaDasJanelasNoHostTests : ReclassificacaoNoHostTestBase
{
    public RotinaDasJanelasNoHostTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "A passada da rotina das janelas processa os avisos da reclassificação")]
    public async Task Passada_ProcessaOsAvisosDaReclassificacao()
    {
        Cenario perfil = await CenarioAsync([Consultar, Sensivel], Leandro);
        await SubirAClasseAsync();
        (await AvisosAsync(perfil.Perfil)).Should().Be(0);
        RelogioComTemporizadorManual relogio = new();
        TaskCompletionSource passada = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using PeriodicJobHostedService rotina = Montar(relogio, passada);

        await rotina.StartAsync(CancellationToken.None);
        await relogio.DispararAsync();
        await passada.Task;
        await rotina.StopAsync(CancellationToken.None);

        (await AvisosAsync(perfil.Perfil)).Should().BeGreaterThan(0);
    }

    private PeriodicJobHostedService Montar(TimeProvider relogio, TaskCompletionSource passada)
    {
        ServiceCollection modulo = new();
        modulo.AddAutorizacaoModule(new ConfigurationBuilder().Build());
        Type tipo = modulo
            .Where(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType?.Name == "JanelasDeMigracaoHostedService")
            .Select(d => d.ImplementationType!)
            .Single();

        return (PeriodicJobHostedService)ActivatorUtilities.CreateInstance(
            Fixture.Factory.Services, tipo, new EscopoQueAvisaOFim(Fixture.Factory.Services.GetRequiredService<IServiceScopeFactory>(), passada), relogio);
    }
}
