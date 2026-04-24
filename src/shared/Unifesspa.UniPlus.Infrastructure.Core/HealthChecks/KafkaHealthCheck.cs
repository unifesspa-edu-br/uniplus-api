namespace Unifesspa.UniPlus.Infrastructure.Core.HealthChecks;

using System.Diagnostics.CodeAnalysis;

using Confluent.Kafka;

using Microsoft.Extensions.Diagnostics.HealthChecks;

public sealed class KafkaHealthCheck : IHealthCheck
{
    private readonly string _bootstrapServers;

    public KafkaHealthCheck(string bootstrapServers)
    {
        _bootstrapServers = bootstrapServers;
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Health check must isolate downstream failures and report Unhealthy instead of propagating exceptions to the readiness pipeline.")]
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            AdminClientConfig config = new() { BootstrapServers = _bootstrapServers };
            using IAdminClient adminClient = new AdminClientBuilder(config).Build();
            Metadata metadata = adminClient.GetMetadata(TimeSpan.FromSeconds(5));
            return Task.FromResult(metadata.Brokers.Count > 0
                ? HealthCheckResult.Healthy("Kafka está acessível.")
                : HealthCheckResult.Unhealthy("Kafka sem brokers disponíveis."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Kafka inacessível.", ex));
        }
    }
}
