namespace Unifesspa.UniPlus.Infrastructure.Core.HealthChecks;

using Confluent.Kafka;

using Microsoft.Extensions.Diagnostics.HealthChecks;

public sealed class KafkaHealthCheck : IHealthCheck
{
    private readonly string _bootstrapServers;

    public KafkaHealthCheck(string bootstrapServers)
    {
        _bootstrapServers = bootstrapServers;
    }

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
#pragma warning disable CA1031 // Captura genérica intencional em health check
        catch (Exception ex)
#pragma warning restore CA1031
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Kafka inacessível.", ex));
        }
    }
}
