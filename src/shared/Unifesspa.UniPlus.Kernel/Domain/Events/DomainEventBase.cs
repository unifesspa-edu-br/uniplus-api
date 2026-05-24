namespace Unifesspa.UniPlus.Kernel.Domain.Events;

// OccurredOn é parâmetro obrigatório do construtor (não inicializador com
// DateTimeOffset.UtcNow): quem levanta o evento provê o instante a partir do
// TimeProvider, mantendo o domínio determinístico. O fitness test
// RelogioViaTimeProviderTests bane leituras diretas de relógio em src/.
// EventId permanece UUID v7 — tornar a IDENTIDADE determinística é escopo
// separado da ADR-0032, não desta convenção de relógio.
public abstract record DomainEventBase(DateTimeOffset OccurredOn) : IDomainEvent
{
    // UUID v7 (ADR-0032) — eventos ganham ordering temporal natural no outbox
    // e nos consumers, facilitando debug e replay determinístico. get-only
    // (sem init): EventId é identidade única por instância — `with` preserva o
    // valor via copy ctor sem permitir override acidental.
    public Guid EventId { get; } = Guid.CreateVersion7();
}
