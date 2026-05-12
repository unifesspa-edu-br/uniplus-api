namespace Unifesspa.UniPlus.Infrastructure.Core.Idempotency;

/// <summary>
/// Configuração do cache de idempotência (ADR-0027). Bind a partir da seção
/// <c>UniPlus:Idempotency</c>; defaults sensatos garantem que projetos sem
/// configuração explícita rodam imediatamente.
/// </summary>
public sealed record IdempotencyOptions
{
    public const string SectionName = "UniPlus:Idempotency";

    /// <summary>
    /// Nome de chave usada para cifrar response body via IUniPlusEncryptionService.
    /// Default alinhado com a key canônica provisionada pelo chart
    /// <c>platform/vault-transit-bootstrap</c> do uniplus-infra (uniplus-infra#219):
    /// <c>uniplus-idempotency-aesgcm</c>. A policy <c>uniplus-api-transit</c> da
    /// role <c>uniplus-api</c> só permite update em encrypt/decrypt desta chave
    /// específica — qualquer nome diferente retorna 403 permission denied.
    /// </summary>
    public const string EncryptionKeyName = "uniplus-idempotency-aesgcm";

    /// <summary>TTL da entrada do cache. ADR-0027 fixa 24h como teto.</summary>
    public TimeSpan Ttl { get; init; } = TimeSpan.FromHours(24);

    /// <summary>Tamanho máximo do request body aceito (rejeita 413 antes do hash).</summary>
    public long MaxBodyBytes { get; init; } = 1L * 1024 * 1024; // 1 MiB
}
