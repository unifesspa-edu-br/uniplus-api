namespace Unifesspa.UniPlus.Kernel.Domain.Events;

public abstract record DomainEventBase : IDomainEvent
{
    // UUID v7 (ADR-0032) — eventos ganham ordering temporal natural no outbox
    // e nos consumers, facilitando debug e replay determinístico.
    public Guid EventId { get; } = Guid.CreateVersion7();
    public DateTimeOffset OccurredOn { get; } = DateTimeOffset.UtcNow;
}
