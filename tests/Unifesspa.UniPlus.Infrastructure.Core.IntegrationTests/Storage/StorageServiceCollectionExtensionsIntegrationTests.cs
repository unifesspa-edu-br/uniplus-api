namespace Unifesspa.UniPlus.Infrastructure.Core.IntegrationTests.Storage;

using System.Text;

using AwesomeAssertions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;

using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;
using Unifesspa.UniPlus.Infrastructure.Core.Storage;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

[Collection(MinioContainerFixture.CollectionName)]
public sealed class StorageServiceCollectionExtensionsIntegrationTests(MinioContainerFixture minio)
{
    private const string TestBucket = "uniplus-storage-tests";

    private ServiceProvider BuildProvider()
    {
        Dictionary<string, string?> config = new()
        {
            ["Storage:Endpoint"] = minio.Endpoint,
            ["Storage:AccessKey"] = MinioContainerFixture.AccessKey,
            ["Storage:SecretKey"] = MinioContainerFixture.SecretKey,
        };

        ServiceCollection services = new();
        services.AddUniPlusStorage(
            new ConfigurationBuilder().AddInMemoryCollection(config).Build(),
            new HostingEnvironment { EnvironmentName = Environments.Production });

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task UploadEDownload_RoundTrip_DevePreservarBytes()
    {
        await using ServiceProvider sp = BuildProvider();
        using IServiceScope scope = sp.CreateScope();
        IStorageService storage = scope.ServiceProvider.GetRequiredService<IStorageService>();

        string objectName = $"smoke/round-trip-{Guid.NewGuid():N}.txt";
        byte[] payload = Encoding.UTF8.GetBytes("conteúdo de smoke — Uni+ #342");

        using (MemoryStream uploadStream = new(payload))
        {
            string location = await storage.UploadAsync(
                TestBucket,
                objectName,
                uploadStream,
                "text/plain; charset=utf-8");

            location.Should().Be($"{TestBucket}/{objectName}");
        }

        await using Stream downloaded = await storage.DownloadAsync(TestBucket, objectName);
        using MemoryStream collector = new();
        await downloaded.CopyToAsync(collector);

        collector.ToArray().Should().Equal(payload);
    }

    [Fact]
    public async Task DownloadLimitado_ObjetoMaiorQueLimite_TruncaNoLimiteSemBaixarOResto()
    {
        await using ServiceProvider sp = BuildProvider();
        using IServiceScope scope = sp.CreateScope();
        IStorageService storage = scope.ServiceProvider.GetRequiredService<IStorageService>();

        string objectName = $"smoke/limitado-{Guid.NewGuid():N}.bin";
        byte[] payload = Encoding.UTF8.GetBytes("0123456789ABCDEFGHIJ"); // 20 bytes
        const long limite = 5;

        using (MemoryStream uploadStream = new(payload))
        {
            await storage.UploadAsync(TestBucket, objectName, uploadStream, "application/octet-stream");
        }

        await using Stream limitado = await storage.DownloadLimitadoAsync(TestBucket, objectName, limite);
        using MemoryStream collector = new();
        await limitado.CopyToAsync(collector);

        // O MinIO só transmite os 5 primeiros bytes (Range request) — o
        // objeto real tem 20, mas nunca chegam a trafegar pela rede/memória.
        collector.ToArray().Should().Equal(payload[..(int)limite]);
    }
}
