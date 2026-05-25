namespace Unifesspa.UniPlus.Spikes.EventSourcing.Application.Portas;

/// <summary>
/// Operação de "direito ao esquecimento" (LGPD) via crypto-shredding: apaga a
/// chave do titular, tornando indecifrável toda a PII cifrada com ela — sem mutar
/// o log append-only de eventos.
/// </summary>
public interface IServicoEsquecimento
{
    /// <summary>Esquece (apaga a chave de) um titular. Idempotente.</summary>
    Task EsquecerAsync(Guid sujeitoId, CancellationToken cancellationToken = default);
}
