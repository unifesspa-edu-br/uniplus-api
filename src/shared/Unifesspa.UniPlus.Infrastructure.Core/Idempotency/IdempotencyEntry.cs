namespace Unifesspa.UniPlus.Infrastructure.Core.Idempotency;

/// <summary>
/// POCO mapeado para a tabela <c>idempotency_cache</c> (ADR-0027). Adicionado
/// ao DbContext do módulo via <c>EfCoreIdempotencyStore&lt;TDbContext&gt;</c>;
/// entries persistidas e atualizadas pelo <c>IdempotencyFilter</c> em
/// transações curtas separadas da transação do agregado (trade-off
/// documentado em ADR-0027 Consequências negativas).
/// </summary>
public sealed class IdempotencyEntry
{
    /// <summary>Identificador interno (UUID v7 para ordering temporal).</summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>
    /// Escopo: combina principal autenticado (sub do JWT) + tenant futuro.
    /// Previne colisão entre clientes que escolham keys idênticas.
    /// </summary>
    public string Scope { get; set; } = string.Empty;

    /// <summary>
    /// Endpoint canônico no formato <c>"METHOD /path/template"</c>
    /// (ex.: <c>"POST /api/editais"</c>). Usa o template da rota, não a URL
    /// instanciada — evita explosão de chaves para path com parâmetros.
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>Header <c>Idempotency-Key</c> tal como recebido do cliente.</summary>
    public string IdempotencyKey { get; set; } = string.Empty;

    /// <summary>SHA-256 hex do request body raw (sem canonicalize).</summary>
    public string BodyHash { get; set; } = string.Empty;

    public IdempotencyStatus Status { get; set; }

    public int? ResponseStatus { get; set; }

    /// <summary>JSON serializado de headers cacheados (Content-Type, Location).</summary>
    public string? ResponseHeadersJson { get; set; }

    /// <summary>
    /// Response body cifrado at-rest via <c>IUniPlusEncryptionService</c> com
    /// chave nomeada <c>"idempotency"</c> (ADR-0027 §"Cifragem at-rest").
    /// </summary>
#pragma warning disable CA1819 // Properties should not return arrays — entidade EF Core mapeia bytea diretamente; record value-equality não se aplica.
    public byte[]? ResponseBodyCipher { get; set; }
#pragma warning restore CA1819

    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>
    /// Sem default — o store sempre seta via <see cref="TimeProvider"/> injetado
    /// para que testes determinísticos (FakeTimeProvider) controlem o instante
    /// de criação. Default zerado pode ser detectado em queries diagnósticas.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
}
