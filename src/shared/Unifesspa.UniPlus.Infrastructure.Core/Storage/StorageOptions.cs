namespace Unifesspa.UniPlus.Infrastructure.Core.Storage;

/// <summary>
/// Bound options for object storage (S3-compatible — MinIO em todos os ambientes Uni+).
/// Ligadas via <see cref="DependencyInjection.StorageServiceCollectionExtensions.AddUniPlusStorage"/>.
/// Fora de Development, <see cref="Endpoint"/>/<see cref="AccessKey"/>/<see cref="SecretKey"/>
/// devem estar preenchidos — caso contrário, o startup falha. Em Development a validação é leniente
/// para permitir bring-up parcial sem MinIO local (ex.: rodar API de auth sem subir storage).
/// </summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>
    /// Endpoint MinIO no formato <c>host:port</c> (sem esquema). O esquema é controlado por
    /// <see cref="UseSSL"/> — combinar HTTPS via flag e não embedar <c>https://</c> aqui.
    /// </summary>
    public string Endpoint { get; init; } = string.Empty;

    /// <summary>Access key (S3 access key id). Provida por env var em prod (chart Helm + Vault).</summary>
    public string AccessKey { get; init; } = string.Empty;

    /// <summary>Secret key (S3 secret access key). Nunca commitar — sempre via env var/Vault.</summary>
    public string SecretKey { get; init; } = string.Empty;

    /// <summary>Quando <see langword="true"/>, conecta via HTTPS. Default <see langword="false"/>.</summary>
    public bool UseSSL { get; init; }

    /// <summary>Região S3 (opcional — MinIO usa <c>us-east-1</c> por default e ignora se vazio).</summary>
    public string? Region { get; init; }

    /// <summary>
    /// Bucket default por API (ex.: <c>uniplus-documentos</c>). Opcional na camada de DI —
    /// handlers que dependem dele devem validar a própria obrigatoriedade.
    /// </summary>
    public string? BucketName { get; init; }
}
