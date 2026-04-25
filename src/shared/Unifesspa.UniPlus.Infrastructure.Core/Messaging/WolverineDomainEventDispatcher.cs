namespace Unifesspa.UniPlus.Infrastructure.Core.Messaging;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Domain.Events;

internal sealed class WolverineDomainEventDispatcher(Wolverine.IMessageBus bus) : IDomainEventDispatcher
{
    // Wolverine.IMessageBus.PublishAsync não aceita CancellationToken (publish é fire-and-forget,
    // cancelamento ocorre no nível do routing). Mantemos o parâmetro ct na assinatura do contrato
    // por consistência com ICommandBus.Send e para evolução futura.
    public Task Publish(IDomainEvent domainEvent, CancellationToken ct = default)
        => bus.PublishAsync(domainEvent).AsTask();
}
