using System.Collections.Concurrent;
using Unifesspa.UniPlus.Spikes.EventSourcing.Application.EventosIntegracao;

namespace Unifesspa.UniPlus.Spikes.EventSourcing.Infrastructure;

/// <summary>
/// Coletor thread-safe de eventos de integração entregues pelo outbox. Singleton
/// usado pelos testes para observar que a mensagem cascateada foi de fato entregue
/// (happy path) ou não (rollback) — sem inspecionar tabelas internas do Wolverine.
/// </summary>
public sealed class ColetorIntegracao
{
    private readonly ConcurrentBag<EditalPublicadoIntegrado> _recebidos = [];

    public void Registrar(EditalPublicadoIntegrado evento)
    {
        ArgumentNullException.ThrowIfNull(evento);
        _recebidos.Add(evento);
    }

    public bool Contem(Guid editalId) => _recebidos.Any(e => e.EditalId == editalId);

    public int Total => _recebidos.Count;
}
