namespace Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Options;

using Minio;

using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;
using Unifesspa.UniPlus.Infrastructure.Core.Storage;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Selecao.Infrastructure.ExternalServices;

/// <summary>
/// O armazenamento real da API sobre o MinIO do container de teste: o registro de produção
/// (<see cref="StorageServiceCollectionExtensions.AddUniPlusStorage"/>) com o relógio do processo,
/// apontado para o bucket privado informado.
/// </summary>
internal sealed class ArmazenamentoDeTesteNoMinio
{
    private readonly ServiceProvider _provedor;

    public ArmazenamentoDeTesteNoMinio(MinioContainerFixture minio, string bucketPrivado)
    {
        ServiceCollection servicos = new();
        servicos.AddSingleton<TimeProvider>(new RelogioEmMicrossegundos(TimeProvider.System));
        servicos.AddUniPlusStorage(
            new ConfigurationBuilder().AddInMemoryCollection(ConfiguracaoDoMinio(minio, bucketPrivado)).Build(),
            new HostingEnvironment { EnvironmentName = Environments.Production });
        _provedor = servicos.BuildServiceProvider();
    }

    public IStorageService Bucketes => _provedor.GetRequiredService<IStorageService>();

    public IOptions<StorageOptions> Opcoes => _provedor.GetRequiredService<IOptions<StorageOptions>>();

    public IMinioClient ClienteInterno =>
        _provedor.GetRequiredKeyedService<IMinioClient>(StorageServiceCollectionExtensions.StorageInternalClientKey);

    public ArquivoArmazenadoStorageService ArquivosArmazenados() => new(Bucketes, Opcoes);

    private static Dictionary<string, string?> ConfiguracaoDoMinio(MinioContainerFixture minio, string bucketPrivado)
    {
        ArgumentNullException.ThrowIfNull(minio);
        return new()
        {
            ["Storage:Endpoint"] = minio.Endpoint,
            ["Storage:AccessKey"] = MinioContainerFixture.AccessKey,
            ["Storage:SecretKey"] = MinioContainerFixture.SecretKey,
            ["Storage:BucketName"] = bucketPrivado,
        };
    }
}
