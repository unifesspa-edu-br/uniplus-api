namespace Unifesspa.UniPlus.Infrastructure.Core.Caching;

/// <summary>
/// Bound options for Redis cache. Ligadas via
/// <see cref="DependencyInjection.CacheServiceCollectionExtensions.AddUniPlusCache"/>.
/// Fora de Development, <see cref="ConnectionString"/> deve estar preenchida — caso contrário,
/// o startup falha. Em Development a validação é leniente para permitir bring-up parcial sem
/// Redis local (ex.: rodar API só com pipeline HTTP/auth sem subir cache).
/// </summary>
public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    /// <summary>
    /// Connection string no formato <c>StackExchange.Redis</c> — host:port, com opções como
    /// <c>password</c>, <c>user</c>, <c>ssl</c>, <c>abortConnect</c>. Provida por env var em
    /// prod (chart Helm + Vault).
    /// </summary>
    public string ConnectionString { get; init; } = string.Empty;
}
