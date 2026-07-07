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

    private ServiceProvider BuildProvider(string? publicEndpoint = null, bool? publicUseSsl = null)
    {
        Dictionary<string, string?> config = new()
        {
            ["Storage:Endpoint"] = minio.Endpoint,
            ["Storage:AccessKey"] = MinioContainerFixture.AccessKey,
            ["Storage:SecretKey"] = MinioContainerFixture.SecretKey,
        };
        if (publicEndpoint is not null)
        {
            config["Storage:PublicEndpoint"] = publicEndpoint;
        }

        if (publicUseSsl is not null)
        {
            config["Storage:PublicUseSSL"] = publicUseSsl.Value.ToString();
        }

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

    [Fact]
    public async Task DownloadLimitado_ObjetoVazio_DevolveStreamVazioSemLancar()
    {
        await using ServiceProvider sp = BuildProvider();
        using IServiceScope scope = sp.CreateScope();
        IStorageService storage = scope.ServiceProvider.GetRequiredService<IStorageService>();

        string objectName = $"smoke/vazio-{Guid.NewGuid():N}.bin";
        using (MemoryStream uploadStream = new([]))
        {
            await storage.UploadAsync(TestBucket, objectName, uploadStream, "application/pdf");
        }

        // Range sobre objeto vazio não é satisfazível (416 do MinIO) — o
        // método trata isso como stream vazio, não como exceção.
        await using Stream limitado = await storage.DownloadLimitadoAsync(TestBucket, objectName, 100);
        using MemoryStream collector = new();
        await limitado.CopyToAsync(collector);

        collector.ToArray().Should().BeEmpty();
    }

    [Fact]
    public async Task GerarUrlUploadTemporariaAsync_ComPublicEndpointConfigurado_AssinaComOEndpointPublico()
    {
        const string publicEndpoint = "storage.uniplus.example.org";
        await using ServiceProvider sp = BuildProvider(publicEndpoint);
        using IServiceScope scope = sp.CreateScope();
        IStorageService storage = scope.ServiceProvider.GetRequiredService<IStorageService>();

        // Devolvida a um cliente externo (browser fora da rede Docker/cluster,
        // ver Storage:PublicEndpoint) — precisa apontar (e estar assinada)
        // para o endpoint público, nunca para o interno (só alcançável de
        // dentro da rede Docker/cluster).
        string url = await storage.GerarUrlUploadTemporariaAsync(
            TestBucket, $"smoke/{Guid.NewGuid():N}.pdf", TimeSpan.FromMinutes(5), "application/pdf");

        url.Should().Contain(publicEndpoint);
        url.Should().NotContain(minio.Endpoint);
    }

    [Fact]
    public async Task GerarUrlUploadTemporariaAsync_ComMesmoEndpointMasSslPublicoDiferente_UsaOEsquemaPublico()
    {
        // Mesmo host interno/público (ex.: exposto por fora via ingress no
        // mesmo hostname), mas esquemas diferentes (HTTP dentro do cluster,
        // HTTPS por fora) — não pode reusar o cliente interno cegamente só
        // porque o endpoint bate.
        await using ServiceProvider sp = BuildProvider(publicEndpoint: minio.Endpoint, publicUseSsl: true);
        using IServiceScope scope = sp.CreateScope();
        IStorageService storage = scope.ServiceProvider.GetRequiredService<IStorageService>();

        string url = await storage.GerarUrlUploadTemporariaAsync(
            TestBucket, $"smoke/{Guid.NewGuid():N}.pdf", TimeSpan.FromMinutes(5), "application/pdf");

        url.Should().StartWith("https://");
    }

    [Fact]
    public async Task GerarUrlUploadTemporariaAsync_ComPublicEndpointVazioMasComSslPublicoDiferente_AssinaComOEndpointInternoEEsquemaPublico()
    {
        // Storage:PublicEndpoint="" (string vazia, não ausente — é o valor
        // literal deixado em branco no values.yaml do Helm chart quando só
        // Storage:PublicUseSSL é preenchido) não pode virar o host assinado:
        // precisa cair para Storage:Endpoint mesmo assim, só trocando o
        // esquema.
        await using ServiceProvider sp = BuildProvider(publicEndpoint: string.Empty, publicUseSsl: true);
        using IServiceScope scope = sp.CreateScope();
        IStorageService storage = scope.ServiceProvider.GetRequiredService<IStorageService>();

        string url = await storage.GerarUrlUploadTemporariaAsync(
            TestBucket, $"smoke/{Guid.NewGuid():N}.pdf", TimeSpan.FromMinutes(5), "application/pdf");

        url.Should().StartWith("https://");
        url.Should().Contain(minio.Endpoint);
    }
}
