namespace Unifesspa.UniPlus.Infrastructure.Common.HealthChecks;

using Microsoft.Extensions.Diagnostics.HealthChecks;

using StackExchange.Redis;

public sealed class RedisHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer _connectionMultiplexer;

    public RedisHealthCheck(IConnectionMultiplexer connectionMultiplexer)
    {
        _connectionMultiplexer = connectionMultiplexer;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            IDatabase database = _connectionMultiplexer.GetDatabase();
            await database.PingAsync().ConfigureAwait(false);
            return HealthCheckResult.Healthy("Redis está acessível.");
        }
#pragma warning disable CA1031 // Captura genérica intencional em health check
        catch (Exception ex)
#pragma warning restore CA1031
        {
            return HealthCheckResult.Unhealthy("Redis inacessível.", ex);
        }
    }
}
