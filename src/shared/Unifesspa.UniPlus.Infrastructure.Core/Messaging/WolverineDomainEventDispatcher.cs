namespace Unifesspa.UniPlus.Infrastructure.Core.Messaging;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Domain.Events;

internal sealed class WolverineDomainEventDispatcher(Wolverine.IMessageBus bus) : IDomainEventDispatcher
{
    // Honramos ct no boundary: se o caller já estiver cancelado, lançamos
    // OperationCanceledException e NÃO publicamos o evento. Isso significa que eventos
    // podem ser suprimidos em cenários de cancelamento — comportamento intencional para
    // o caso de uso de fire-and-forget direto. Se o evento representa fato consumado em
    // transação, use entity.AddDomainEvent(...) + SaveChangesAsync — o outbox do
    // Wolverine garante atomicidade write+evento sem passar por aqui. Ver XMLDoc do
    // IDomainEventDispatcher para a regra completa.
    //
    // Nota: Wolverine.IMessageBus.PublishAsync não aceita CancellationToken (publish é
    // fire-and-forget no nível do bus); só protegemos o boundary de entrada.
    public Task Publish(IDomainEvent domainEvent, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return bus.PublishAsync(domainEvent).AsTask();
    }
}
