namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

using Domain.Events;

/// <summary>
/// Coletor in-memory de <see cref="ProcessoPublicadoEvent"/> usado pelos
/// testes de outbox cascading (ADR-0005, Story #759 T4 #785) para verificar
/// que o handler subscritor recebeu o evento drenado pela configuração
/// produtiva (PG queue → listener no mesmo host).
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "Singleton resolvido via DI nos handlers convencionais do Wolverine; precisa ser público.")]
public sealed class DomainEventCollector
{
    private readonly ConcurrentQueue<ProcessoPublicadoEvent> _eventos = new();

    public void Record(ProcessoPublicadoEvent @event) => _eventos.Enqueue(@event);

    public IReadOnlyCollection<ProcessoPublicadoEvent> Snapshot() => [.. _eventos];

    public void Clear()
    {
        while (_eventos.TryDequeue(out _))
        {
            // Drena a fila — usar Clear não é threadsafe contra Enqueue concorrente.
        }
    }
}
