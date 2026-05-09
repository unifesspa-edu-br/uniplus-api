namespace Unifesspa.UniPlus.Infrastructure.Core.IntegrationTests.HealthChecks;

using AwesomeAssertions;

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

using Microsoft.Extensions.Diagnostics.HealthChecks;

using Minio;

using Unifesspa.UniPlus.Infrastructure.Core.HealthChecks;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

[Collection(MinioContainerFixture.CollectionName)]
public sealed class MinioHealthCheckTests(MinioContainerFixture minio)
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "Caller awaits using on the returned IMinioClient (cast to IAsyncDisposable).")]
    private static IMinioClient BuildClient(string endpoint, string accessKey, string secretKey) =>
        new MinioClient()
            .WithEndpoint(endpoint)
            .WithCredentials(accessKey, secretKey)
            .WithSSL(false)
            .Build();

    [Fact]
    public async Task CheckHealthAsync_MinioAcessivel_RetornaHealthy()
    {
        IMinioClient client = BuildClient(minio.Endpoint, MinioContainerFixture.AccessKey, MinioContainerFixture.SecretKey);
        using ((IDisposable)client)
        {
            MinioHealthCheck check = new(client);

            HealthCheckResult result = await check.CheckHealthAsync(new HealthCheckContext());

            result.Status.Should().Be(HealthStatus.Healthy);
            result.Description.Should().Contain("acessível");
        }
    }

    [Fact]
    public async Task CheckHealthAsync_MinioInacessivel_RetornaUnhealthy()
    {
        // Endpoint inválido (porta 1 — fechada). Confirma que o catch-all do health check
        // converte exceção em Unhealthy em vez de propagar para o pipeline de readiness.
        IMinioClient client = BuildClient("127.0.0.1:1", "any", "any");
        using ((IDisposable)client)
        {
            MinioHealthCheck check = new(client);

            HealthCheckResult result = await check.CheckHealthAsync(new HealthCheckContext());

            result.Status.Should().Be(HealthStatus.Unhealthy);
            result.Description.Should().Contain("inacessível");
            result.Exception.Should().NotBeNull();
        }
    }
}
