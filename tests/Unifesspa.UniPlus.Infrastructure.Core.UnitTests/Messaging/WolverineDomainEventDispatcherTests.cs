namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Messaging;

using FluentAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Infrastructure.Core.Messaging;
using Unifesspa.UniPlus.Kernel.Domain.Events;

public class WolverineDomainEventDispatcherTests
{
    [Fact]
    public async Task Publish_ComCancellationTokenJaCancelado_DeveLancarOperationCanceledException()
    {
        // Wolverine.IMessageBus.PublishAsync não aceita CancellationToken nativamente.
        // O wrapper precisa honrar o ct no boundary para evitar publicação silenciosa
        // quando o caller já cancelou — comportamento que um leitor da assinatura
        // razoavelmente espera. Sem este guard, Publish(evt, alreadyCancelled) publica.
        Wolverine.IMessageBus bus = Substitute.For<Wolverine.IMessageBus>();
        WolverineDomainEventDispatcher dispatcher = new(bus);
        IDomainEvent evento = Substitute.For<IDomainEvent>();

        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        Func<Task> acao = () => dispatcher.Publish(evento, cts.Token);

        await acao.Should().ThrowAsync<OperationCanceledException>();
        await bus.DidNotReceive().PublishAsync(Arg.Any<IDomainEvent>());
    }

    [Fact]
    public async Task Publish_ComCancellationTokenAtivo_DeveDelegarParaWolverineMessageBus()
    {
        Wolverine.IMessageBus bus = Substitute.For<Wolverine.IMessageBus>();
        WolverineDomainEventDispatcher dispatcher = new(bus);
        IDomainEvent evento = Substitute.For<IDomainEvent>();

        await dispatcher.Publish(evento, CancellationToken.None);

        await bus.Received(1).PublishAsync(evento);
    }
}
