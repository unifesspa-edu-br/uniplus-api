namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

using Unifesspa.UniPlus.Selecao.Domain.Events;

/// <summary>
/// Coletor in-memory de domain events usado pelos testes de outbox cascading
/// para verificar que o handler subscritor recebeu o evento drenado pela
/// configuração produtiva (PG queue → listener no mesmo host).
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "Singleton resolvido via DI nos handlers convencionais do Wolverine; precisa ser público.")]
public sealed class DomainEventCollector
{
    private readonly ConcurrentQueue<EditalPublicadoEvent> _eventos = new();

    public void Record(EditalPublicadoEvent @event) => _eventos.Enqueue(@event);

    public IReadOnlyCollection<EditalPublicadoEvent> Snapshot() => [.. _eventos];

    public void Clear()
    {
        while (_eventos.TryDequeue(out _))
        {
            // Drena a fila — usar Clear não é threadsafe contra Enqueue concorrente.
        }
    }
}
