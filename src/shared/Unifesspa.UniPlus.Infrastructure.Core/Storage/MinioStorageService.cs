namespace Unifesspa.UniPlus.Infrastructure.Core.Storage;

using Minio;
using Minio.DataModel;
using Minio.DataModel.Args;
using Minio.Exceptions;

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

    public async Task<string> GerarUrlUploadTemporariaAsync(string bucket, string nomeArquivo, TimeSpan expiracao, string contentType, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        // Diferente de GerarUrlTemporariaAsync (GET, assume bucket já existente): este é o
        // primeiro write path que não passa por UploadAsync — o bucket pode ainda não existir
        // quando o primeiro upload direto acontece.
        await GarantirBucketExisteAsync(bucket, cancellationToken).ConfigureAwait(false);

        PresignedPutObjectArgs args = new PresignedPutObjectArgs()
            .WithBucket(bucket)
            .WithObject(nomeArquivo)
            .WithExpiry((int)expiracao.TotalSeconds)
            .WithHeaders(new Dictionary<string, string> { ["Content-Type"] = contentType });

        return await _minioClient.PresignedPutObjectAsync(args).ConfigureAwait(false);
    }

    public async Task<ObjetoMetadados?> ObterMetadadosAsync(string bucket, string nomeArquivo, CancellationToken cancellationToken = default)
    {
        try
        {
            StatObjectArgs args = new StatObjectArgs()
                .WithBucket(bucket)
                .WithObject(nomeArquivo);

            ObjectStat stat = await _minioClient.StatObjectAsync(args, cancellationToken).ConfigureAwait(false);
            return new ObjetoMetadados(stat.Size, stat.ContentType);
        }
        catch (ObjectNotFoundException)
        {
            return null;
        }
        catch (BucketNotFoundException)
        {
            return null;
        }
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
