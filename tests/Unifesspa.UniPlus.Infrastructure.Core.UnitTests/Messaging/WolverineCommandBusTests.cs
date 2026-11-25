namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Messaging;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Infrastructure.Core.Messaging;

public class WolverineCommandBusTests
{
    [Fact]
    public async Task Send_DeveDelegarParaWolverineMessageBusInvokeAsync()
    {
        // Protege o contrato do wrapper: Send delega exatamente para InvokeAsync<T>
        // do Wolverine.IMessageBus, propagando o command e o ct. Se alguém trocar
        // por SendAsync, PublishAsync ou outro método, este teste quebra antes de
        // chegar em integração.
        const string respostaEsperada = "ok";
        TestCommand command = new(Guid.NewGuid());
        Wolverine.IMessageBus bus = BusDeTeste.ComResposta(command, respostaEsperada);

        WolverineCommandBus wrapper = new(bus);

        string resposta = await wrapper.Send(command, CancellationToken.None);

        resposta.Should().Be(respostaEsperada);
        await bus.Received(1).InvokeAsync<string>(command, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Send_DevePropagarCancellationToken()
    {
        TestCommand command = new(Guid.NewGuid());
        Wolverine.IMessageBus bus = BusDeTeste.ComResposta(command, "ok");

        WolverineCommandBus wrapper = new(bus);
        using CancellationTokenSource cts = new();
        CancellationToken ct = cts.Token;

        await wrapper.Send(command, ct);

        await bus.Received(1).InvokeAsync<string>(command, ct);
    }

    private sealed record TestCommand(Guid Id) : ICommand<string>;
}
