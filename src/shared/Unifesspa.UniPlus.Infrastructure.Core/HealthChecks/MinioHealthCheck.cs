namespace Unifesspa.UniPlus.Infrastructure.Core.HealthChecks;

using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using Minio;

using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;

public sealed class MinioHealthCheck : IHealthCheck
{
    private readonly IMinioClient _minioClient;

    public MinioHealthCheck(
        [FromKeyedServices(StorageServiceCollectionExtensions.StorageInternalClientKey)] IMinioClient minioClient)
    {
        _minioClient = minioClient;
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Health check must isolate downstream failures and report Unhealthy instead of propagating exceptions to the readiness pipeline.")]
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // ListBucketsAsync é a operação mais leve para confirmar conectividade + credentials —
            // valida endpoint, TLS (se UseSSL), e access/secret key sem depender de bucket
            // específico existir.
            await _minioClient.ListBucketsAsync(cancellationToken).ConfigureAwait(false);
            return HealthCheckResult.Healthy("MinIO está acessível.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("MinIO inacessível.", ex);
        }
    }
}
