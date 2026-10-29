namespace Unifesspa.UniPlus.Verificador.UnitTests;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Unifesspa.UniPlus.Verificador.Configuracao;
using Unifesspa.UniPlus.Verificador.Contratos;

using Wolverine;
using Wolverine.Configuration;
using Wolverine.RabbitMQ.Internal;
using Wolverine.Runtime;

public sealed class ConfiguracaoDoWolverineTests
{
    [Fact(DisplayName = "A fila de pedidos tem um listener inline por slot de paralelismo")]
    public void FilaDePedidos_TemUmListenerPorSlotDeParalelismo()
    {
        using IHost host = Host.CreateDefaultBuilder()
            .UseWolverine(opcoes =>
            {
                ConfiguracaoDoWolverine.Configurar(opcoes, new VerificadorOptions { Paralelismo = 4, RabbitMq = { Host = "rabbit" } });
            })
            .Build();

        IWolverineRuntime runtime = host.Services.GetRequiredService<IWolverineRuntime>();
        Endpoint fila = runtime.Options.Transports.GetOrCreate<RabbitMqTransport>().Queues[FilasDaVerificacao.Pedidos];

        fila.Compile(runtime);

        fila.ListenerCount.Should().Be(4);
        fila.Mode.Should().Be(EndpointMode.Inline);
    }

    [Theory(DisplayName = "As duas filas de resultados são quorum com envio inline")]
    [InlineData(FilasDaVerificacao.Resultados)]
    [InlineData(FilasDaVerificacao.ResultadosAutorizacao)]
    public void FilasDeResultados_SaoQuorumComEnvioInline(string nome)
    {
        using IHost host = Host.CreateDefaultBuilder()
            .UseWolverine(opcoes =>
            {
                ConfiguracaoDoWolverine.Configurar(opcoes, new VerificadorOptions { RabbitMq = { Host = "rabbit" } });
            })
            .Build();

        IWolverineRuntime runtime = host.Services.GetRequiredService<IWolverineRuntime>();
        RabbitMqQueue fila = runtime.Options.Transports.GetOrCreate<RabbitMqTransport>().Queues[nome];

        fila.QueueType.Should().Be(QueueType.quorum);
        fila.Mode.Should().Be(EndpointMode.Inline);
    }
}
