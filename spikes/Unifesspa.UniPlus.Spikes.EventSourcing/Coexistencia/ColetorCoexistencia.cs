using System.Collections.Concurrent;

namespace Unifesspa.UniPlus.Spikes.EventSourcing.Coexistencia;

/// <summary>
/// Coletor thread-safe que conta entregas por id. A contagem permite asserir
/// entrega (Contagem &gt; 0) e exatamente-uma-vez (Contagem == 1) num cluster.
/// </summary>
public sealed class ColetorCoexistencia
{
    private readonly ConcurrentDictionary<Guid, int> _contagem = new();

    public void Registrar(Guid id) => _contagem.AddOrUpdate(id, 1, (_, n) => n + 1);

    public int Contagem(Guid id) => _contagem.TryGetValue(id, out int n) ? n : 0;

    public bool Contem(Guid id) => Contagem(id) > 0;
}
