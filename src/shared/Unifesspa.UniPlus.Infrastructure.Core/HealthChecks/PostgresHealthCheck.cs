namespace Unifesspa.UniPlus.Infrastructure.Core.HealthChecks;

using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Diagnostics.HealthChecks;

using Npgsql;

public sealed class PostgresHealthCheck : IHealthCheck
{
    private readonly string _connectionString;

    public PostgresHealthCheck(string connectionString)
    {
        _connectionString = connectionString;
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Health check must isolate downstream failures and report Unhealthy instead of propagating exceptions to the readiness pipeline.")]
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            NpgsqlConnection connection = new(_connectionString);
            await using (connection.ConfigureAwait(false))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                NpgsqlCommand command = connection.CreateCommand();
                await using (command.ConfigureAwait(false))
                {
                    command.CommandText = "SELECT 1";
                    await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                }
            }

            return HealthCheckResult.Healthy("PostgreSQL está acessível.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL inacessível.", ex);
        }
    }
}
