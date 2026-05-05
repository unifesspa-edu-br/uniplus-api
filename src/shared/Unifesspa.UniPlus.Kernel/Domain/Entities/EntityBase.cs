namespace Unifesspa.UniPlus.Kernel.Domain.Entities;

using Events;

public abstract class EntityBase
{
    // UUIDv7 (RFC 9562 §5.7) — 48 bits de unix_ts_ms no prefixo + 74 bits aleatórios.
    // Adotado em todas as entidades de domínio (ADR-0032). Ganhos:
    // (a) ordering temporal estável → cursor pagination (ADR-0026) ganha semântica
    //     "próxima página = criados depois" sem código adicional;
    // (b) localidade B-tree no Postgres → menos page splits e bloat em tabelas de
    //     alto volume (Inscricao, outbox, audit);
    // (c) coerência com o resto do projeto que já usa Guid.CreateVersion7 em
    //     Instance/traceId de ProblemDetails.
    // O timestamp leak não cria risco LGPD novo: toda entidade já carrega
    // CreatedAt como audit trail obrigatório, expondo o mesmo dado.
    public Guid Id { get; protected init; } = Guid.CreateVersion7();
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
