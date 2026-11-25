namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Messaging;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;

using Wolverine;

/// <summary>
/// O pipeline Wolverine real entrega o comando ao handler descoberto por convenção.
/// </summary>
public sealed class WolverineCommandBusEndToEndTests
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
            await VerificarRespostaAsync(host);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    private static async Task VerificarRespostaAsync(IHost host)
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        Application.Abstractions.Messaging.ICommandBus bus =
            scope.ServiceProvider.GetRequiredService<Application.Abstractions.Messaging.ICommandBus>();

        string resposta = await bus.Send(new PingCommand("uniplus"));

        resposta.Should().Be("pong:uniplus");
    }

}

// O código gerado pelo Wolverine precisa acessar os tipos publicamente.
public sealed record PingCommand(string Quem) : ICommand<string>;

public static class PingHandler
{
    public static string Handle(PingCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return $"pong:{command.Quem}";
    }
}
