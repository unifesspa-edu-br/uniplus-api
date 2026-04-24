namespace Unifesspa.UniPlus.Infrastructure.Core.HealthChecks;

using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Diagnostics.HealthChecks;

using StackExchange.Redis;

public sealed class RedisHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer _connectionMultiplexer;

    public RedisHealthCheck(IConnectionMultiplexer connectionMultiplexer)
    {
        _connectionMultiplexer = connectionMultiplexer;
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Health check must isolate downstream failures and report Unhealthy instead of propagating exceptions to the readiness pipeline.")]
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            IDatabase database = _connectionMultiplexer.GetDatabase();
            await database.PingAsync().ConfigureAwait(false);
            return HealthCheckResult.Healthy("Redis está acessível.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Redis inacessível.", ex);
        }
    }
}
