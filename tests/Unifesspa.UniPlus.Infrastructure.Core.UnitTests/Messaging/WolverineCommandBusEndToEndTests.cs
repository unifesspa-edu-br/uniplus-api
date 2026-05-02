namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Messaging;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;

using Wolverine;

/// <summary>
/// Backbone Wolverine vivo: <see cref="ICommandBus.Send"/> percorre o pipeline
/// Wolverine real e entrega no handler descoberto por convenção. Não testa
/// outbox/atomicidade de domain events — esse invariante é responsabilidade
/// de issue futura dedicada (ver spike branch spike/135-outbox-validation).
/// </summary>
/// <remarks>
/// <para>
/// Este teste sobe um <see cref="IHost"/> Wolverine real para exercitar o
/// pipeline ponta-a-ponta — formalmente é mais largo que um unit test, mas
/// permanece neste projeto por (a) não ter I/O externo (sem banco, sem rede),
/// (b) startup &lt; 1 s e (c) ser o único caminho para validar que a convenção
/// de descoberta de handler funciona com a wrapper <see cref="WolverineCommandBus"/>.
/// </para>
/// </remarks>
public class WolverineCommandBusEndToEndTests
{
    [Fact]
    public async Task ICommandBus_Send_DespachaParaHandlerEHonraResposta()
    {
        IHostBuilder hostBuilder = Host.CreateDefaultBuilder()
            .UseWolverine(opts => opts.Discovery.IncludeType(typeof(PingHandler)))
            .ConfigureServices(services => services.AddWolverineMessaging());

        using IHost host = hostBuilder.Build();
        await host.StartAsync();
        try
        {
            await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
            // Fully-qualify para desambiguar do Wolverine.ICommandBus (do using Wolverine).
            Application.Abstractions.Messaging.ICommandBus bus =
                scope.ServiceProvider.GetRequiredService<Application.Abstractions.Messaging.ICommandBus>();

            string resposta = await bus.Send(new PingCommand("uniplus"));

            resposta.Should().Be("pong:uniplus");
        }
        finally
        {
            await host.StopAsync();
        }
    }

}

// PingCommand e PingHandler são public top-level por exigência do code generator
// do Wolverine: o pipeline gerado em runtime referencia esses tipos diretamente,
// e tipos internal/nested levam a CS0122 ("inacessível devido ao seu nível de
// proteção") na compilação dinâmica. Nada incomum — ver guia Golden Path.
[SuppressMessage("Performance", "CA1515:Consider making public types internal",
    Justification = "Wolverine code generator exige tipo public para gerar o pipeline.")]
public sealed record PingCommand(string Quem) : ICommand<string>;

[SuppressMessage("Performance", "CA1515:Consider making public types internal",
    Justification = "Wolverine code generator exige tipo public para gerar o pipeline.")]
public static class PingHandler
{
    [SuppressMessage("Design", "CA1062:Validate arguments of public methods", Justification = "Handler do teste.")]
    public static string Handle(PingCommand command) => $"pong:{command.Quem}";
}
