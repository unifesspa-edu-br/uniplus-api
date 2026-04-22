namespace Unifesspa.UniPlus.Kernel.Domain.Events;

public interface IDomainEvent
{
    Guid EventId { get; }
    DateTimeOffset OccurredOn { get; }
}
