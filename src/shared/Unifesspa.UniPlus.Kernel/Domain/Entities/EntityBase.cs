namespace Unifesspa.UniPlus.Kernel.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Events;

public abstract class EntityBase
{
    public Guid Id { get; protected init; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; protected set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; protected set; }
    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public string? DeletedBy { get; private set; }

    public void MarkAsDeleted(string deletedBy)
    {
        IsDeleted = true;
        DeletedAt = DateTimeOffset.UtcNow;
        DeletedBy = deletedBy;
    }

    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void AddDomainEvent(IDomainEvent domainEvent) =>
        _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();

    // Snapshot atômico dos domain events seguido de limpeza da coleção
    // interna. Uso canônico no caminho cascading messages do Wolverine
    // (handlers que retornam IEnumerable<object>): drena os eventos da
    // entidade no mesmo ponto em que o handler os entrega ao bus,
    // evitando republicação acidental se o agregado sobreviver ao escopo
    // do handler (cache, sagas, processadores long-lived).
    public IReadOnlyCollection<IDomainEvent> DequeueDomainEvents()
    {
        IDomainEvent[] snapshot = [.. _domainEvents];
        _domainEvents.Clear();
        return snapshot;
    }
}
