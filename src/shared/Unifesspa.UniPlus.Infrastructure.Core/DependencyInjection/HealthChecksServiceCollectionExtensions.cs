namespace Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using Unifesspa.UniPlus.Infrastructure.Core.HealthChecks;
using Unifesspa.UniPlus.Infrastructure.Core.Messaging;

/// <summary>
/// Registra health checks agregados para Postgres, Redis, MinIO e Kafka. Pareado com a
/// configuração existente de <see cref="Authentication.OidcAuthenticationConfiguration"/>
/// (que já registra <c>OidcDiscoveryHealthCheck</c>) para compor o readiness probe completo
/// das APIs Uni+.
/// </summary>
public static class HealthChecksServiceCollectionExtensions
{
    /// <summary>
    /// Tag canônica usada para filtrar checks que respondem ao readiness probe. Endpoints
    /// devem usar <c>Predicate = verificacao => verificacao.Tags.Contains(<see cref="ReadyTag"/>)</c> em
    /// <see cref="Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions"/>.
    /// </summary>
    public const string ReadyTag = "ready";

    private const string ChaveDoRedis = $"{Caching.RedisOptions.SectionName}:{nameof(Caching.RedisOptions.ConnectionString)}";
    private const string ChaveDoMinio = $"{Storage.StorageOptions.SectionName}:{nameof(Storage.StorageOptions.Endpoint)}";
    private const string ChaveDoKafka = $"{KafkaSettings.SectionName}:{nameof(KafkaSettings.BootstrapServers)}";

    /// <summary>
    /// Registra verificações para as dependências de infraestrutura das APIs Uni+ no
    /// <see cref="IHealthChecksBuilder"/> existente. Cada verificação é ativada condicionalmente
    /// pela presença da configuração correspondente — em Development sem alguma dependência, ela
    /// simplesmente não é registrada, em vez de reportar Unhealthy permanente.
    /// </summary>
    /// <param name="services">A coleção de serviços.</param>
    /// <param name="configuration">Configuração da aplicação.</param>
    /// <param name="connectionStringName">Nome da connection string Postgres (PortalDb,
    /// UniPlusDb).</param>
    /// <returns>A própria <paramref name="services"/> para encadeamento fluente.</returns>
    /// <remarks>
    /// <para>
    /// Pré-requisitos por check:
    /// </para>
    /// <list type="bullet">
    ///   <item><description><b>Postgres</b>: usa a connection string nominal — ativa quando
    ///     <c>ConnectionStrings:{connectionStringName}</c> está preenchido.</description></item>
    ///   <item><description><b>Redis</b>: depende de <see cref="StackExchange.Redis.IConnectionMultiplexer"/>
    ///     registrado por <see cref="CacheServiceCollectionExtensions.AddUniPlusCache"/>.
    ///     Ativa quando <c>Redis:ConnectionString</c> está preenchido.</description></item>
    ///   <item><description><b>MinIO</b>: depende de <see cref="Minio.IMinioClient"/>
    ///     registrado por <see cref="StorageServiceCollectionExtensions.AddUniPlusStorage"/>.
    ///     Ativa quando <c>Storage:Endpoint</c> está preenchido.</description></item>
    ///   <item><description><b>Kafka</b>: depende de <see cref="KafkaSettings"/> bind por
    ///     <see cref="WolverineOutboxConfiguration.UseWolverineOutboxCascading"/>. Ativa quando
    ///     <c>Kafka:BootstrapServers</c> está preenchido.</description></item>
    /// </list>
    /// </remarks>
    public static IServiceCollection AddUniPlusHealthChecks(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionStringName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionStringName);

        IHealthChecksBuilder healthChecks = services.AddHealthChecks();

        AddPostgresIfConfigured(healthChecks, configuration, connectionStringName);
        AddIfConfigured<RedisHealthCheck>(healthChecks, configuration, ChaveDoRedis, "redis", "cache");
        AddIfConfigured<MinioHealthCheck>(healthChecks, configuration, ChaveDoMinio, "minio", "storage");
        AddIfConfigured<KafkaHealthCheck>(healthChecks, configuration, ChaveDoKafka, "kafka", "messaging");

        return services;
    }

    private static void AddPostgresIfConfigured(
        IHealthChecksBuilder healthChecks, IConfiguration configuration, string connectionStringName)
    {
        string? conexao = configuration.GetConnectionString(connectionStringName);
        if (!string.IsNullOrWhiteSpace(conexao))
        {
            healthChecks.AddCheck(
                name: "postgres",
                instance: new PostgresHealthCheck(conexao),
                tags: [ReadyTag, "db"]);
        }
    }

    private static void AddIfConfigured<THealthCheck>(
        IHealthChecksBuilder healthChecks, IConfiguration configuration, string chave, string nome, string tag)
        where THealthCheck : class, IHealthCheck
    {
        if (!string.IsNullOrWhiteSpace(configuration[chave]))
        {
            healthChecks.AddCheck<THealthCheck>(name: nome, tags: [ReadyTag, tag]);
        }
    }
}
