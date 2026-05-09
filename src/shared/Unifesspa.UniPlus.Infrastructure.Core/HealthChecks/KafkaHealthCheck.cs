namespace Unifesspa.UniPlus.Infrastructure.Core.HealthChecks;

using System.Diagnostics.CodeAnalysis;

using Confluent.Kafka;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Core.Messaging;

public sealed class KafkaHealthCheck : IHealthCheck
{
    private readonly KafkaSettings _settings;

    public KafkaHealthCheck(IOptions<KafkaSettings> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _settings = options.Value;
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Health check must isolate downstream failures and report Unhealthy instead of propagating exceptions to the readiness pipeline.")]
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            AdminClientConfig config = new() { BootstrapServers = _settings.BootstrapServers };

            // Aplica SecurityProtocol/SaslMechanism/SaslUsername/SaslPassword/SslCa* quando
            // configurados — necessário em standalone (SASL_SSL + SCRAM-SHA-512). Sem isto, o
            // health check em standalone tentaria PLAINTEXT contra um broker SASL e reportaria
            // Unhealthy permanente, mascarando o estado real.
            KafkaSecurity.Apply(config, _settings);

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
