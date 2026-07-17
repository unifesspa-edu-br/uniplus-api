namespace Unifesspa.UniPlus.Infrastructure.Core.Storage;

using System.Net;
using System.Net.Http.Headers;

using Microsoft.Extensions.DependencyInjection;

using Minio;
using Minio.DataModel;
using Minio.DataModel.Args;
using Minio.Exceptions;

using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;

public sealed class MinioStorageService : IStorageService
{
    private readonly IMinioClient _minioClient;
    private readonly IMinioClient _presignClient;
    private readonly IHttpClientFactory _httpClientFactory;

    public MinioStorageService(
        [FromKeyedServices(StorageServiceCollectionExtensions.StorageInternalClientKey)] IMinioClient minioClient,
        [FromKeyedServices(StorageServiceCollectionExtensions.StoragePublicClientKey)] IMinioClient presignClient,
        IHttpClientFactory httpClientFactory)
    {
        _minioClient = minioClient;
        _presignClient = presignClient;
        _httpClientFactory = httpClientFactory;
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

    public async Task<Stream> DownloadLimitadoAsync(string bucket, string nomeArquivo, long limiteBytes, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limiteBytes);

        // GetObjectAsync (API de alto nível, usada por DownloadAsync) valida o
        // download como completo e lança PartialContentException quando recebe
        // menos bytes que o Content-Length total do objeto — não aceita um
        // Range GET deliberadamente parcial. Uma URL pre-assinada de GET com
        // header Range via HttpClient é o caminho que o MinIO honra de fato: o
        // servidor nunca transmite mais que o intervalo pedido.
        PresignedGetObjectArgs presignedArgs = new PresignedGetObjectArgs()
            .WithBucket(bucket)
            .WithObject(nomeArquivo)
            .WithExpiry(60);
        string url = await _minioClient.PresignedGetObjectAsync(presignedArgs).ConfigureAwait(false);

        using HttpClient httpClient = _httpClientFactory.CreateClient(nameof(MinioStorageService));
        using HttpRequestMessage request = new(HttpMethod.Get, url);
        request.Headers.Range = new RangeHeaderValue(0, limiteBytes - 1);

        using HttpResponseMessage response = await httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        // A ObjectKey de staging segue sobrescrevível até o TTL da URL de
        // upload expirar — mesmo já tendo passado pelo stat com tamanho > 0,
        // o objeto pode ter virado 0 bytes até este GET rodar. Um Range sobre
        // objeto vazio não é satisfazível (416); trata como stream vazio em
        // vez de deixar EnsureSuccessStatusCode() escapar como erro não
        // tratado — ValidarConteudo já sabe recusar conteúdo vazio (422).
        if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
        {
            return new MemoryStream();
        }

        response.EnsureSuccessStatusCode();

        MemoryStream memoryStream = new();
        Stream responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (responseStream.ConfigureAwait(false))
        {
            await responseStream.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);
        }

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

        // _presignClient (não _minioClient): a URL vai para um cliente externo
        // (browser fora da rede Docker/cluster) — precisa ser assinada com o
        // endpoint público (ver Storage:PublicEndpoint em StorageOptions).
        return await _presignClient.PresignedGetObjectAsync(args).ConfigureAwait(false);
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

        // _presignClient: mesma razão de GerarUrlTemporariaAsync — o upload
        // direto é feito pelo cliente externo, então a URL precisa ser
        // alcançável e assinada por ele (endpoint público).
        //
        // Limitação conhecida do SDK Minio (achado de revisão, smoke test manual): a URL
        // pré-assinada resultante traz um parâmetro de query espúrio
        // `content-type=Minio.DataModel.Args.PresignedPutObjectArgs` — o SDK (todas as
        // versões lançadas até 7.0.0, ObjectOperations.PresignedPutObjectAsync) passa
        // `Convert.ToString(args.GetType())` (o NOME DO TIPO .NET) onde deveria passar o
        // metaData real; o `Content-Type` de `WithHeaders` acima nunca chega lá. Inofensivo
        // na prática: `X-Amz-SignedHeaders=host` só assina o header `Host`, então o servidor
        // MinIO nunca valida esse parâmetro — confirmado que o PUT funciona normalmente com
        // o `Content-Type` real enviado pelo cliente. NÃO reescrever a URL para remover o
        // parâmetro: ele faz parte do canonical request da assinatura SigV4 (confirmado por
        // teste: removê-lo devolve 403 SignatureDoesNotMatch). Sem correção disponível via
        // versão do pacote — o `master` do SDK reescreveu esse trecho por completo, mas
        // ainda não tem release; nenhuma versão publicada (6.x/7.0.0) está livre do bug.
        return await _presignClient.PresignedPutObjectAsync(args).ConfigureAwait(false);
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
