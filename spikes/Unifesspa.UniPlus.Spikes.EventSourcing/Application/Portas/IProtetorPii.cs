using Unifesspa.UniPlus.Spikes.EventSourcing.Domain;

namespace Unifesspa.UniPlus.Spikes.EventSourcing.Application.Portas;

/// <summary>
/// Porta de proteção de PII para o payload append-only. Cifra o ator antes do
/// append e tenta revelá-lo na leitura. O "esquecimento" (crypto-shredding) é
/// responsabilidade do adaptador de infraestrutura, que controla as chaves.
/// </summary>
public interface IProtetorPii
{
    /// <summary>Cifra o ator, garantindo a chave do titular. Para uso antes do append.</summary>
    Task<AtorCifrado> ProtegerAsync(Ator ator, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tenta revelar o ator cifrado. Retorna <c>null</c> quando a chave do titular
    /// já foi esquecida (crypto-shredding) — o fato permanece, a PII não.
    /// </summary>
    Task<Ator?> TentarRevelarAsync(AtorCifrado cifrado, CancellationToken cancellationToken = default);
}
