namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Messaging;

using System.Diagnostics.CodeAnalysis;

using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;

using Wolverine;

using AppCommandBus = Unifesspa.UniPlus.Application.Abstractions.Messaging.ICommandBus;
using AppICommand = Unifesspa.UniPlus.Application.Abstractions.Messaging.ICommand<string>;

/// <summary>
/// Backbone Wolverine vivo: <see cref="AppCommandBus.Send"/> percorre o pipeline
/// Wolverine real e entrega no handler descoberto por convenção. Não testa
/// outbox/atomicidade de domain events — esse invariante é responsabilidade
/// de issue futura dedicada (ver spike branch spike/135-outbox-validation).
/// </summary>
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
            AppCommandBus bus = scope.ServiceProvider.GetRequiredService<AppCommandBus>();

            string resposta = await bus.Send(new PingCommand("uniplus"));

            resposta.Should().Be("pong:uniplus");
        }
        finally
        {
            await host.StopAsync();
        }
    }
}

[SuppressMessage("Performance", "CA1515:Consider making public types internal",
    Justification = "Wolverine convenção exige discovery por reflection.")]
public sealed record PingCommand(string Quem) : AppICommand;

[SuppressMessage("Performance", "CA1515:Consider making public types internal",
    Justification = "Wolverine convenção exige discovery por reflection.")]
public static class PingHandler
{
    [SuppressMessage("Design", "CA1062:Validate arguments of public methods", Justification = "Handler do teste.")]
    public static string Handle(PingCommand command) => $"pong:{command.Quem}";
}
