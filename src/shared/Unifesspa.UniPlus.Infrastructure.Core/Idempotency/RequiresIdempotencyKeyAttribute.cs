namespace Unifesspa.UniPlus.Infrastructure.Core.Idempotency;

/// <summary>
/// Sinaliza que o endpoint exige header <c>Idempotency-Key</c> (ADR-0027).
/// Aplicado em métodos de controllers que executam comandos críticos
/// (POST/PATCH não-idempotentes por semântica). O <c>IdempotencyFilter</c>
/// detecta o atributo via metadata da action, valida o header, faz
/// lookup/store e short-circuit em caso de replay ou body mismatch.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class RequiresIdempotencyKeyAttribute : Attribute
{
    /// <summary>
    /// Override opcional do TTL da entrada de cache para este endpoint, em
    /// segundos. <c>-1</c> (default) usa <see cref="IdempotencyOptions.Ttl"/>
    /// (teto de 24h, ADR-0027) — tipos de atributo não aceitam <c>int?</c>,
    /// então <c>-1</c> é o sentinel de "não configurado" (nenhum TTL real é
    /// negativo). Existe para o caso em que a resposta cacheada carrega um
    /// artefato com validade própria mais curta que 24h (ex.: URL
    /// pre-assinada de upload) — sem o override, um replay depois da URL
    /// expirar devolveria uma URL inutilizável, sem meio do cliente reobter
    /// uma válida sem gerar outro registro pendente com nova Idempotency-Key.
    /// </summary>
    public int TtlSeconds { get; init; } = -1;
}
