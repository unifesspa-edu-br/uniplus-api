namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Storage;

using Microsoft.Extensions.Options;

using Minio;
using Minio.DataModel.Args;

using NSubstitute;

using Unifesspa.UniPlus.Infrastructure.Core.Storage;

public sealed class MinioStorageServiceTests
{
    [Fact]
    public async Task GerarUrlUploadTemporaria_SemCriarBucketAusente_AssinaSemConsultarOBucket()
    {
        IMinioClient cliente = Substitute.For<IMinioClient>();
        MinioStorageService storage = new(
            cliente,
            cliente,
            Substitute.For<IHttpClientFactory>(),
            Options.Create(new StorageOptions { CriaBucketAusente = false }),
            TimeProvider.System);

        await storage.GerarUrlUploadTemporariaAsync("bucket-provisionado", "objeto", TimeSpan.FromMinutes(5), "application/pdf");

        await cliente.DidNotReceive().BucketExistsAsync(Arg.Any<BucketExistsArgs>(), Arg.Any<CancellationToken>());
        await cliente.Received(1).PresignedPutObjectAsync(Arg.Any<PresignedPutObjectArgs>());
    }
}
