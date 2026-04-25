namespace Unifesspa.UniPlus.Application.Abstractions.Messaging;

using Unifesspa.UniPlus.Kernel.Domain.Events;

/// <summary>
/// Publica domain events para os handlers registrados, intra-módulo (in-process) ou
/// cross-módulo (via Kafka transport configurado no Wolverine). Aplicação publica eventos
/// como <see cref="IDomainEvent"/> — tópicos, partições e headers são detalhe de routing
/// gerenciado pelo Wolverine. Ver ADR-022 (uniplus-docs/docs/adrs/).
/// </summary>
public interface IDomainEventDispatcher
{
    Task Publish(IDomainEvent domainEvent, CancellationToken ct = default);
}
