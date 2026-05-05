namespace Unifesspa.UniPlus.Infrastructure.Core.Idempotency;

/// <summary>
/// Configuração do cache de idempotência (ADR-0027). Bind a partir da seção
/// <c>UniPlus:Idempotency</c>; defaults sensatos garantem que projetos sem
/// configuração explícita rodam imediatamente.
/// </summary>
public sealed record IdempotencyOptions
{
    public const string SectionName = "UniPlus:Idempotency";

    /// <summary>Nome de chave usada para cifrar response body via IUniPlusEncryptionService.</summary>
    public const string EncryptionKeyName = "idempotency";

    /// <summary>TTL da entrada do cache. ADR-0027 fixa 24h como teto.</summary>
    public TimeSpan Ttl { get; init; } = TimeSpan.FromHours(24);

    /// <summary>Tamanho máximo do request body aceito (rejeita 413 antes do hash).</summary>
    public long MaxBodyBytes { get; init; } = 1L * 1024 * 1024; // 1 MiB
}
