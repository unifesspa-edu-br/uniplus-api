namespace Unifesspa.UniPlus.Discentes.UnitTests.Sincronizacao;

using AwesomeAssertions;

using JasperFx.CodeGeneration.Model;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Discentes.API;
using Unifesspa.UniPlus.Discentes.Domain.Entities;
using Unifesspa.UniPlus.Discentes.Domain.Enums;
using Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa;
using Unifesspa.UniPlus.Discentes.Infrastructure.Sincronizacao;
using Unifesspa.UniPlus.Discentes.UnitTests.Sigaa;

using Wolverine;

/// <summary>
/// Exercita o handler da sincronização pelo Wolverine real.
/// </summary>
/// <remarks>
/// O gerador de código do Wolverine monta o pipeline na subida. Se faltar a declaração de
/// que alguma porta do módulo vem do contêiner, é aqui que a montagem falha — e não em
/// tempo de execução, na primeira sincronização de madrugada. Testes com substitutos
/// diretos do handler não passam por esse caminho.
/// </remarks>
public sealed class PipelineWolverineDaSincronizacaoTests
{
    private static readonly DateOnly Dia = new(2026, 9, 2);

    [Fact]
    public async Task Wolverine_monta_o_pipeline_e_despacha_a_sincronizacao()
    {
        ExecucoesEmMemoria execucoes = new();
        using IHost host = await SubirAsync(execucoes);

        try
        {
            await host.Services.GetRequiredService<IMessageBus>()
                .InvokeAsync(new SincronizarVinculosDiscentes(Dia));

            execucoes.Registradas.Should().ContainSingle();
            execucoes.Registradas[0].Status.Should().Be(SyncRunStatus.Completed);
            execucoes.Registradas[0].DataDeReferencia.Should().Be(Dia);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    public async Task Execucao_que_falha_fica_registrada_como_fracassada()
    {
        // Sem isto, uma falha deixaria a execução presa em andamento para sempre — e é ela
        // que a próxima execução consulta para saber se o dia já foi tratado.
        ExecucoesEmMemoria execucoes = new();
        using IHost host = await SubirAsync(execucoes, origemQueFalha: true);

        try
        {
            Func<Task> sincronizar = () => host.Services.GetRequiredService<IMessageBus>()
                .InvokeAsync(new SincronizarVinculosDiscentes(Dia));

            await sincronizar.Should().ThrowAsync<Exception>();

            execucoes.Registradas.Should().ContainSingle();
            execucoes.Registradas[0].Status.Should().Be(SyncRunStatus.Failed);
            execucoes.Registradas[0].FinishedAt.Should().NotBeNull();
        }
        finally
        {
            await host.StopAsync();
        }
    }

    private static async Task<IHost> SubirAsync(
        ExecucoesEmMemoria execucoes,
        bool origemQueFalha = false)
    {
        OrigemSimulada origem = new();
        if (!origemQueFalha)
        {
            origem.ResponderPorIngresso();
        }

        IHost host = Host.CreateDefaultBuilder()
            .UseWolverine(opts =>
            {
                opts.Discovery.IncludeType(typeof(SincronizarVinculosDiscentesHandler));

                // Mesma política do host: o gerador não pode recorrer ao contêiner por
                // conta própria. Sem isto, o teste passaria mesmo faltando a declaração
                // das portas — e o defeito só apareceria na primeira sincronização real.
                opts.ServiceLocationPolicy = ServiceLocationPolicy.NotAllowed;

                DiscentesCodegenRegistration.ConfigurarCodegenWolverine(opts);
            })
            .ConfigureServices(services =>
            {
                services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
                services.AddSingleton<TimeProvider>(
                    new RelogioControlado(new DateTimeOffset(2026, 9, 2, 3, 0, 0, TimeSpan.Zero)));
                // Registradas por fábrica, como no host: a unidade de trabalho encaminha
                // para a mesma instância que os repositórios usam, e o gerador não tem
                // como enxergar o tipo concreto por trás disso.
                services.AddSingleton(execucoes);
                services.AddScoped<IRegistroDeExecucoes>(sp => sp.GetRequiredService<ExecucoesEmMemoria>());
                services.AddSingleton<ISigaaVinculoDiscenteClient>(
                    origemQueFalha ? new OrigemQueFalha() : origem);
                services.AddSingleton<IGravadorDeVinculos, GravadorSimulado>();
                services.AddSingleton(Options.Create(new SincronizacaoOptions()));
                services.AddScoped<OrquestradorDeSincronizacao>();
            })
            .Build();

        await host.StartAsync();
        return host;
    }
}
