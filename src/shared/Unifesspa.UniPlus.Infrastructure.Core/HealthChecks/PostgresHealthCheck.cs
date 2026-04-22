namespace Unifesspa.UniPlus.Infrastructure.Core.HealthChecks;

using Microsoft.Extensions.Diagnostics.HealthChecks;

using Npgsql;

public sealed class PostgresHealthCheck : IHealthCheck
{
    private readonly string _connectionString;

    public PostgresHealthCheck(string connectionString)
    {
        _connectionString = connectionString;
    }

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
#pragma warning disable CA1031 // Captura genérica intencional em health check
        catch (Exception ex)
#pragma warning restore CA1031
        {
            return HealthCheckResult.Unhealthy("PostgreSQL inacessível.", ex);
        }
    }
}
