namespace Unifesspa.UniPlus.Infrastructure.Core.Messaging;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Domain.Events;

internal sealed class WolverineDomainEventDispatcher(Wolverine.IMessageBus bus) : IDomainEventDispatcher
{
    // Wolverine.IMessageBus.PublishAsync não aceita CancellationToken (publish é fire-and-forget,
    // cancelamento ocorre no nível do routing). Honramos o ct no boundary do método para que
    // caller que já cancelou veja OperationCanceledException em vez de publicação silenciosa.
    public Task Publish(IDomainEvent domainEvent, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return bus.PublishAsync(domainEvent).AsTask();
    }
}
