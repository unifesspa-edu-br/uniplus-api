namespace Unifesspa.UniPlus.Spikes.EventSourcing.Infrastructure.Persistencia;

/// <summary>
/// Chave simétrica por titular, persistida fora do stream de eventos. Apagá-la é
/// o crypto-shredding (gate G3).
/// <para>
/// No spike a chave vive numa coleção Marten para simplicidade; em produção ela
/// deveria residir num cofre dedicado (ex.: Vault/KMS), nunca no mesmo banco dos
/// eventos — registrado como recomendação no relatório de findings.
/// </para>
/// </summary>
// Público por exigência do Marten: a geração dinâmica de código do storage roda
// num assembly separado que não enxerga tipos internos.
public sealed class ChaveTitular
{
    /// <summary>Identidade da chave (referenciada por <c>AtorCifrado.ChaveId</c>).</summary>
    public Guid Id { get; init; }

    /// <summary>Titular dono da chave. Esquecer o titular apaga todas as suas chaves.</summary>
    public Guid SujeitoId { get; init; }

    /// <summary>Chave AES-256 (32 bytes) em Base64.</summary>
    public string Chave { get; init; } = string.Empty;
}
