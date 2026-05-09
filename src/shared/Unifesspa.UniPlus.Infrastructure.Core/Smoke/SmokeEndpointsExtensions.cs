namespace Unifesspa.UniPlus.Infrastructure.Core.Smoke;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

using StackExchange.Redis;

using Unifesspa.UniPlus.Infrastructure.Core.Caching;
using Unifesspa.UniPlus.Infrastructure.Core.Storage;

using Wolverine;

/// <summary>
/// Endpoints smoke E2E para validação ponta-a-ponta de Storage (MinIO), Cache (Redis) e
/// Messaging (Wolverine outbox + transport). Mapeados sob <c>/api/_smoke</c> e protegidos
/// por papel <c>admin</c> via policy <c>RequireRole</c>.
/// </summary>
/// <remarks>
/// <para>
/// Em produção esses endpoints permanecem ativos para diagnóstico operacional (on-call,
/// validação pós-deploy). Considerar feature flag para desabilitar em hardening posterior.
/// </para>
/// <para>
/// Authorization: <c>RequireAuthorization(policy =&gt; policy.RequireRole("admin"))</c>. O
/// claim role é populado pela <c>KeycloakRolesClaimsTransformation</c> (registrada por
/// <c>AddOidcAuthentication</c>) que mapeia o claim Keycloak <c>realm_access.roles</c> para
/// <c>ClaimTypes.Role</c>. A policy roda na pipeline de AuthZ middleware ANTES do model
/// binding — anônimos recebem 401, autenticados sem role admin recebem 403, sem hidratar
/// IFormFile/etc.
/// </para>
/// </remarks>
public static class SmokeEndpointsExtensions
{
    public const string AdminRole = "admin";
    public const string SmokeBucketFallback = "uniplus-smoke";
    public const string SmokeCacheKeyPrefix = "smoke:";
    private static readonly TimeSpan DefaultCacheTtl = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Mapeia endpoints smoke sob <c>/api/_smoke</c>. Idempotente — chamar duas vezes
    /// causaria conflito de rotas, então deve ser chamado uma única vez por pipeline.
    /// </summary>
    public static IEndpointRouteBuilder MapUniPlusSmokeEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        RouteGroupBuilder group = app
            .MapGroup("/api/_smoke")
            .RequireAuthorization(policy => policy.RequireRole(AdminRole))
            .WithTags("_smoke");

        // Storage: PUT em bucket configurado, retorna location.
        group.MapPost("/storage/upload", UploadSmokeStorageAsync)
            .DisableAntiforgery()
            .WithName("smokeStorageUpload")
            .WithSummary("Smoke E2E — Storage upload")
            .WithDescription("Faz upload de um arquivo no bucket configurado para validar conectividade + credentials do MinIO. Restrito a usuários com role admin.");

        // Cache: SET (random key + UTC now) com TTL 5min, retorna value para verificação.
        group.MapGet("/cache/{key}", ProbeSmokeCacheAsync)
            .WithName("smokeCacheProbe")
            .WithSummary("Smoke E2E — Cache probe")
            .WithDescription("Faz SET/GET de uma chave temporária no Redis com TTL 5min para validar conectividade. Restrito a usuários com role admin.");

        // Messaging: publica SmokePingMessage via IMessageBus — handler em Infrastructure.Core
        // confirma round-trip via log.
        group.MapPost("/messaging/publish", PublishSmokeMessageAsync)
            .WithName("smokeMessagingPublish")
            .WithSummary("Smoke E2E — Messaging publish")
            .WithDescription("Publica um SmokePingMessage via Wolverine outbox para validar persistência + transport (PG queue ou Kafka). O handler em Infrastructure.Core registra log do round-trip. Restrito a usuários com role admin.");

        return app;
    }

    private static async Task<IResult> UploadSmokeStorageAsync(
        IFormFile file,
        IStorageService storage,
        IOptions<StorageOptions> storageOptions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(storageOptions);

        string bucket = string.IsNullOrWhiteSpace(storageOptions.Value.BucketName)
            ? SmokeBucketFallback
            : storageOptions.Value.BucketName;

        string objectName = $"smoke/{Guid.NewGuid():N}-{Path.GetFileName(file.FileName)}";

        Stream stream = file.OpenReadStream();
        await using (stream.ConfigureAwait(false))
        {
            string location = await storage
                .UploadAsync(bucket, objectName, stream, file.ContentType, cancellationToken)
                .ConfigureAwait(false);

            return Results.Ok(new { bucket, objectName, location });
        }
    }

    private static async Task<IResult> ProbeSmokeCacheAsync(
        string key,
        IConnectionMultiplexer redis,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(redis);
        cancellationToken.ThrowIfCancellationRequested();

        IDatabase db = redis.GetDatabase();
        string fullKey = SmokeCacheKeyPrefix + key;
        string value = DateTimeOffset.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture);

        await db.StringSetAsync(fullKey, value, DefaultCacheTtl).ConfigureAwait(false);
        RedisValue retrieved = await db.StringGetAsync(fullKey).ConfigureAwait(false);

        return Results.Ok(new
        {
            key = fullKey,
            value = retrieved.ToString(),
            ttlSeconds = (int)DefaultCacheTtl.TotalSeconds,
        });
    }

    private static async Task<IResult> PublishSmokeMessageAsync(
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bus);

        SmokePingMessage message = new(Guid.NewGuid(), DateTimeOffset.UtcNow);
        await bus.PublishAsync(message).ConfigureAwait(false);

        return Results.Ok(new { id = message.Id, timestamp = message.Timestamp });
    }
}
