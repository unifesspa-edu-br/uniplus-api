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
        ServiceCollection services = new();
        services.AddSingleton(TimeProvider.System);
        services.AddUniPlusStorage(
            new ConfigurationBuilder().AddInMemoryCollection(Configuracao(publicEndpoint, publicUseSsl)).Build(),
            new HostingEnvironment { EnvironmentName = Environments.Production });

        return services.BuildServiceProvider();
    }

    private Dictionary<string, string?> Configuracao(string? publicEndpoint, bool? publicUseSsl)
    {
        Dictionary<string, string?> config = ConfiguracaoDoMinio();
        if (publicEndpoint is not null)
        {
            config["Storage:PublicEndpoint"] = publicEndpoint;
        }

        if (publicUseSsl is not null)
        {
            config["Storage:PublicUseSSL"] = publicUseSsl.Value.ToString();
        }

        return config;
    }

    private Dictionary<string, string?> ConfiguracaoDoMinio() => new()
    {
        ["Storage:Endpoint"] = minio.Endpoint,
        ["Storage:AccessKey"] = MinioContainerFixture.AccessKey,
        ["Storage:SecretKey"] = MinioContainerFixture.SecretKey,
    };

    [Fact]
    public async Task UploadEDownload_RoundTrip_DevePreservarBytes()
    {
        await using ServiceProvider sp = BuildProvider();
        using IServiceScope scope = sp.CreateScope();
        IStorageService storage = Armazenamento(scope);
        string objectName = $"smoke/round-trip-{Guid.NewGuid():N}.txt";
        byte[] payload = Encoding.UTF8.GetBytes("conteúdo de smoke — Uni+ #342");

        string location = await EnviarAsync(storage, objectName, payload, "text/plain; charset=utf-8");
        byte[] baixado = await LerAsync(await storage.DownloadAsync(TestBucket, objectName));

        location.Should().Be($"{TestBucket}/{objectName}");
        baixado.Should().Equal(payload);
    }

    /// <summary>
    /// O MinIO só transmite os 5 primeiros bytes (Range request) — o objeto real tem 20, mas nunca chegam a trafegar
    /// pela rede/memória.
    /// </summary>
    [Fact]
    public async Task DownloadLimitado_ObjetoMaiorQueLimite_TruncaNoLimiteSemBaixarOResto()
    {
        await using ServiceProvider sp = BuildProvider();
        using IServiceScope scope = sp.CreateScope();
        IStorageService storage = Armazenamento(scope);
        string objectName = $"smoke/limitado-{Guid.NewGuid():N}.bin";
        byte[] payload = Encoding.UTF8.GetBytes("0123456789ABCDEFGHIJ"); // 20 bytes
        const long limite = 5;
        await EnviarAsync(storage, objectName, payload, "application/octet-stream");

        byte[] baixado = await LerAsync(await storage.DownloadLimitadoAsync(TestBucket, objectName, limite));

        baixado.Should().Equal(payload[..(int)limite]);
    }

    /// <summary>
    /// Range sobre objeto vazio não é satisfazível (416 do MinIO) — o método trata isso como stream vazio, não como
    /// exceção.
    /// </summary>
    [Fact]
    public async Task DownloadLimitado_ObjetoVazio_DevolveStreamVazioSemLancar()
    {
        await using ServiceProvider sp = BuildProvider();
        using IServiceScope scope = sp.CreateScope();
        IStorageService storage = Armazenamento(scope);
        string objectName = $"smoke/vazio-{Guid.NewGuid():N}.bin";
        await EnviarAsync(storage, objectName, [], "application/pdf");

        byte[] baixado = await LerAsync(await storage.DownloadLimitadoAsync(TestBucket, objectName, 100));

        baixado.Should().BeEmpty();
    }

    /// <summary>
    /// A URL é devolvida a um cliente externo (browser fora da rede Docker/cluster, ver Storage:PublicEndpoint):
    /// precisa apontar (e estar assinada) para o endpoint público, nunca para o interno, só alcançável de dentro da
    /// rede Docker/cluster.
    /// </summary>
    [Fact]
    public async Task GerarUrlUploadTemporariaAsync_ComPublicEndpointConfigurado_AssinaComOEndpointPublico()
    {
        const string publicEndpoint = "storage.uniplus.example.org";

        string url = await UrlDeUploadAsync(publicEndpoint);

        url.Should().Contain(publicEndpoint);
        url.Should().NotContain(minio.Endpoint);
    }

    /// <summary>
    /// Mesmo host interno/público (ex.: exposto por fora via ingress no mesmo hostname), mas esquemas diferentes
    /// (HTTP dentro do cluster, HTTPS por fora) — não pode reusar o cliente interno cegamente só porque o endpoint bate.
    /// </summary>
    [Fact]
    public async Task GerarUrlUploadTemporariaAsync_ComMesmoEndpointMasSslPublicoDiferente_UsaOEsquemaPublico()
    {
        string url = await UrlDeUploadAsync(publicEndpoint: minio.Endpoint, publicUseSsl: true);

        url.Should().StartWith("https://");
    }

    /// <summary>
    /// Storage:PublicEndpoint="" (string vazia, não ausente — é o valor literal deixado em branco no values.yaml do
    /// Helm chart quando só Storage:PublicUseSSL é preenchido) não pode virar o host assinado: precisa cair para
    /// Storage:Endpoint mesmo assim, só trocando o esquema.
    /// </summary>
    [Fact]
    public async Task GerarUrlUploadTemporariaAsync_ComPublicEndpointVazioMasComSslPublicoDiferente_AssinaComOEndpointInternoEEsquemaPublico()
    {
        string url = await UrlDeUploadAsync(publicEndpoint: string.Empty, publicUseSsl: true);

        url.Should().StartWith("https://");
        url.Should().Contain(minio.Endpoint);
    }

    private static IStorageService Armazenamento(IServiceScope escopo) =>
        escopo.ServiceProvider.GetRequiredService<IStorageService>();

    private static async Task<string> EnviarAsync(IStorageService storage, string objectName, byte[] conteudo, string contentType)
    {
        using MemoryStream stream = new(conteudo);

        return await storage.UploadAsync(TestBucket, objectName, stream, contentType);
    }

    private static async Task<byte[]> LerAsync(Stream origem)
    {
        await using (origem)
        {
            using MemoryStream coletor = new();
            await origem.CopyToAsync(coletor);

            return coletor.ToArray();
        }
    }

    private async Task<string> UrlDeUploadAsync(string? publicEndpoint = null, bool? publicUseSsl = null)
    {
        await using ServiceProvider sp = BuildProvider(publicEndpoint, publicUseSsl);
        using IServiceScope scope = sp.CreateScope();

        return await Armazenamento(scope).GerarUrlUploadTemporariaAsync(
            TestBucket, $"smoke/{Guid.NewGuid():N}.pdf", TimeSpan.FromMinutes(5), "application/pdf");
    }
}
