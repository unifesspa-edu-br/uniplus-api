namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Messaging;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Infrastructure.Core.Messaging;

public class WolverineQueryBusTests
{
    [Fact]
    public async Task Send_DeveDelegarParaWolverineMessageBusInvokeAsync()
    {
        // Protege o contrato do wrapper: Send delega exatamente para InvokeAsync<T>
        // do Wolverine.IMessageBus, propagando a query e o ct. Se alguém trocar
        // por SendAsync, PublishAsync ou outro método, este teste quebra antes
        // de chegar em integração.
        const string respostaEsperada = "ok";
        Wolverine.IMessageBus bus = Substitute.For<Wolverine.IMessageBus>();
        TestQuery query = new(Guid.NewGuid());
        bus.InvokeAsync<string>(query, Arg.Any<CancellationToken>())
           .Returns(Task.FromResult(respostaEsperada));

        WolverineQueryBus wrapper = new(bus);

        string resposta = await wrapper.Send(query, CancellationToken.None);

        resposta.Should().Be(respostaEsperada);
        await bus.Received(1).InvokeAsync<string>(query, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Send_DevePropagarCancellationToken()
    {
        Wolverine.IMessageBus bus = Substitute.For<Wolverine.IMessageBus>();
        TestQuery query = new(Guid.NewGuid());
        bus.InvokeAsync<string>(query, Arg.Any<CancellationToken>())
           .Returns(Task.FromResult("ok"));

        WolverineQueryBus wrapper = new(bus);
        using CancellationTokenSource cts = new();
        CancellationToken ct = cts.Token;

        await wrapper.Send(query, ct);

        await bus.Received(1).InvokeAsync<string>(query, ct);
    }

    private sealed record TestQuery(Guid Id) : IQuery<string>;
}
