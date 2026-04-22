namespace Unifesspa.UniPlus.Infrastructure.Core.Storage;

using Minio;
using Minio.DataModel.Args;

public sealed class MinioStorageService : IStorageService
{
    private readonly IMinioClient _minioClient;

    public MinioStorageService(IMinioClient minioClient)
    {
        _minioClient = minioClient;
    }

    public async Task<string> UploadAsync(string bucket, string nomeArquivo, Stream conteudo, string contentType, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conteudo);

        await GarantirBucketExisteAsync(bucket, cancellationToken).ConfigureAwait(false);

        PutObjectArgs args = new PutObjectArgs()
            .WithBucket(bucket)
            .WithObject(nomeArquivo)
            .WithStreamData(conteudo)
            .WithObjectSize(conteudo.Length)
            .WithContentType(contentType);

        await _minioClient.PutObjectAsync(args, cancellationToken).ConfigureAwait(false);
        return $"{bucket}/{nomeArquivo}";
    }

    public async Task<Stream> DownloadAsync(string bucket, string nomeArquivo, CancellationToken cancellationToken = default)
    {
        MemoryStream memoryStream = new();
        GetObjectArgs args = new GetObjectArgs()
            .WithBucket(bucket)
            .WithObject(nomeArquivo)
            .WithCallbackStream(stream => stream.CopyTo(memoryStream));

        await _minioClient.GetObjectAsync(args, cancellationToken).ConfigureAwait(false);
        memoryStream.Position = 0;
        return memoryStream;
    }

    public async Task RemoverAsync(string bucket, string nomeArquivo, CancellationToken cancellationToken = default)
    {
        RemoveObjectArgs args = new RemoveObjectArgs()
            .WithBucket(bucket)
            .WithObject(nomeArquivo);

        await _minioClient.RemoveObjectAsync(args, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> GerarUrlTemporariaAsync(string bucket, string nomeArquivo, TimeSpan expiracao, CancellationToken cancellationToken = default)
    {
        PresignedGetObjectArgs args = new PresignedGetObjectArgs()
            .WithBucket(bucket)
            .WithObject(nomeArquivo)
            .WithExpiry((int)expiracao.TotalSeconds);

        return await _minioClient.PresignedGetObjectAsync(args).ConfigureAwait(false);
    }

    private async Task GarantirBucketExisteAsync(string bucket, CancellationToken cancellationToken)
    {
        bool existe = await _minioClient.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucket), cancellationToken).ConfigureAwait(false);
        if (!existe)
        {
            await _minioClient.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucket), cancellationToken).ConfigureAwait(false);
        }
    }
}
