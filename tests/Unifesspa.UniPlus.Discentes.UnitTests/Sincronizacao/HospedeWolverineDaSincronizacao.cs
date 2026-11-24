namespace Unifesspa.UniPlus.Discentes.UnitTests.Sincronizacao;

using JasperFx.CodeGeneration.Model;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Discentes.API;
using Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa;
using Unifesspa.UniPlus.Discentes.Infrastructure.Sincronizacao;
using Unifesspa.UniPlus.Discentes.UnitTests.Sigaa;

using Wolverine;

/// <summary>
/// Hospedeiro com o Wolverine real, montado como o do módulo, mas com origem, gravador e
/// registro de execuções controlados pelo teste.
/// </summary>
internal sealed class HospedeWolverineDaSincronizacao : IAsyncDisposable
{
    private readonly IHost _host;

    private HospedeWolverineDaSincronizacao(IHost host, ExecucoesEmMemoria execucoes)
    {
        _host = host;
        Execucoes = execucoes;
    }

    public ExecucoesEmMemoria Execucoes { get; }

    public static async Task<HospedeWolverineDaSincronizacao> SubirAsync(bool origemQueFalha = false)
    {
        ExecucoesEmMemoria execucoes = new();
        ISigaaVinculoDiscenteClient origem = origemQueFalha ? new OrigemQueFalha() : new OrigemSimulada();

        IHost host = Host.CreateDefaultBuilder()
            .UseWolverine(ConfigurarWolverine)
            .ConfigureServices(services => RegistrarServicos(services, execucoes, origem))
            .Build();

        await host.StartAsync();
        return new HospedeWolverineDaSincronizacao(host, execucoes);
    }

    public Task SincronizarAsync(DateOnly dataDeReferencia) =>
        _host.Services.GetRequiredService<IMessageBus>()
            .InvokeAsync(new SincronizarVinculosDiscentes(dataDeReferencia));

    public async ValueTask DisposeAsync()
    {
        await _host.StopAsync();
        _host.Dispose();
    }

    private static void ConfigurarWolverine(WolverineOptions opts)
    {
        opts.Discovery.IncludeType(typeof(SincronizarVinculosDiscentesHandler));

        // Mesma política do host: o gerador não pode recorrer ao contêiner por
        // conta própria. Sem isto, o teste passaria mesmo faltando a declaração
        // das portas — e o defeito só apareceria na primeira sincronização real.
        opts.ServiceLocationPolicy = ServiceLocationPolicy.NotAllowed;

        DiscentesCodegenRegistration.ConfigurarCodegenWolverine(opts);
    }

    private static void RegistrarServicos(
        IServiceCollection services,
        ExecucoesEmMemoria execucoes,
        ISigaaVinculoDiscenteClient origem)
    {
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton<TimeProvider>(
            new RelogioControlado(new DateTimeOffset(2026, 9, 2, 3, 0, 0, TimeSpan.Zero)));

        // Registradas por fábrica, como no host: a unidade de trabalho encaminha
        // para a mesma instância que os repositórios usam, e o gerador não tem
        // como enxergar o tipo concreto por trás disso.
        services.AddSingleton(execucoes);
        services.AddScoped<IRegistroDeExecucoes>(sp => sp.GetRequiredService<ExecucoesEmMemoria>());
        services.AddSingleton(origem);
        services.AddSingleton<IGravadorDeVinculos, GravadorSimulado>();
        services.AddSingleton(Options.Create(new SincronizacaoOptions()));
        services.AddScoped<OrquestradorDeSincronizacao>();
    }
}
