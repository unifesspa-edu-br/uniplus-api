namespace Unifesspa.UniPlus.Infrastructure.Core.Storage;

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Minio;
using Minio.DataModel;
using Minio.DataModel.Args;
using Minio.Exceptions;

using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;

public sealed class MinioStorageService : IStorageService
{
    /// <summary>
    /// Cache perpétuo: o endereço do objeto público é derivado do conteúdo, e o mesmo endereço nunca
    /// serve outro conteúdo.
    /// </summary>
    private const string CacheControlDoObjetoImutavel = "public, max-age=31536000, immutable";

    private const string PrefixoDeMetadado = "x-amz-meta-";

    private readonly IMinioClient _minioClient;
    private readonly IMinioClient _presignClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<StorageOptions> _opcoes;
    private readonly TimeProvider _relogio;

    public MinioStorageService(
        [FromKeyedServices(StorageServiceCollectionExtensions.StorageInternalClientKey)] IMinioClient minioClient,
        [FromKeyedServices(StorageServiceCollectionExtensions.StoragePublicClientKey)] IMinioClient presignClient,
        IHttpClientFactory httpClientFactory,
        IOptions<StorageOptions> opcoes,
        TimeProvider relogio)
    {
        _minioClient = minioClient;
        _presignClient = presignClient;
        _httpClientFactory = httpClientFactory;
        _opcoes = opcoes;
        _relogio = relogio;
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

        try
        {
            await _minioClient.RemoveObjectAsync(args, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ObjectNotFoundException or BucketNotFoundException)
        {
            // Objeto ou bucket ausente é o estado que a remoção pede: o S3 já responde sucesso à
            // chave inexistente, e esta exceção só vem do servidor que a sinaliza ou do bucket
            // ausente — a mesma semântica de ObterMetadadosAsync.
        }
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

    public async Task<bool> CopiarComoObjetoPublicoAsync(
        string bucketOrigem,
        string chaveOrigem,
        string bucketDestino,
        string chaveDestino,
        ApresentacaoDoObjetoPublico apresentacao,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bucketOrigem);
        ArgumentException.ThrowIfNullOrWhiteSpace(chaveOrigem);
        ArgumentException.ThrowIfNullOrWhiteSpace(bucketDestino);
        ArgumentException.ThrowIfNullOrWhiteSpace(chaveDestino);
        ArgumentNullException.ThrowIfNull(apresentacao);

        // Consulta seguida de gravação, sem condição atômica: duas cópias concorrentes da mesma
        // chave podem ambas não encontrar o objeto e gravar. A corrida é inofensiva porque a chave
        // é derivada do conteúdo e da procedência — as duas gravariam os mesmos bytes com os mesmos
        // cabeçalhos. Bucket de destino ausente também chega aqui como "não existe", e a cópia
        // abaixo falha por ele, sem criá-lo.
        if (await ObterMetadadosAsync(bucketDestino, chaveDestino, cancellationToken).ConfigureAwait(false) is not null)
        {
            return false;
        }

        // Substituição, e não cópia, dos metadados: o objeto de origem foi gravado sem cabeçalho de
        // apresentação, e herdá-lo entregaria o arquivo com a chave como nome e sem política de cache.
        Dictionary<string, string> cabecalhos = new(StringComparer.OrdinalIgnoreCase)
        {
            ["x-amz-copy-source"] = AssinaturaS3V4.CaminhoDoObjeto(bucketOrigem, chaveOrigem),
            ["x-amz-metadata-directive"] = "REPLACE",
            ["content-type"] = apresentacao.ContentType,
            ["cache-control"] = CacheControlDoObjetoImutavel,
            ["content-disposition"] = ContentDispositionDeAnexo(apresentacao.NomeDeApresentacao),
        };

        foreach ((string nome, string valor) in apresentacao.Procedencia)
        {
            cabecalhos[PrefixoDeMetadado + nome] = valor;
        }

        await CopiarNoServidorAsync(bucketDestino, chaveDestino, cabecalhos, cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// A cópia no servidor (<c>CopyObject</c> do S3) por requisição assinada aqui, e não pela
    /// biblioteca do MinIO: ela recusa enviar <c>Content-Disposition</c> — só monta, de cabeçalho de
    /// conteúdo, o tipo, o tamanho e o MD5 —, e sem ele o cidadão baixaria um arquivo cujo nome é o
    /// hash. Os bytes continuam sem passar pela aplicação.
    /// </summary>
    private async Task CopiarNoServidorAsync(
        string bucket,
        string chave,
        IReadOnlyDictionary<string, string> cabecalhos,
        CancellationToken cancellationToken)
    {
        StorageOptions opcoes = _opcoes.Value;
        Uri endereco = new($"{(opcoes.UseSSL ? "https" : "http")}://{opcoes.Endpoint}{AssinaturaS3V4.CaminhoDoObjeto(bucket, chave)}");
        (string authorization, string amzDate) = AssinaturaS3V4.Assinar(
            HttpMethod.Put, endereco, cabecalhos, opcoes, _relogio.GetUtcNow());

        using HttpRequestMessage requisicao = new(HttpMethod.Put, endereco) { Content = new ByteArrayContent([]) };
        requisicao.Headers.TryAddWithoutValidation("Authorization", authorization);
        requisicao.Headers.TryAddWithoutValidation("x-amz-date", amzDate);
        requisicao.Headers.TryAddWithoutValidation("x-amz-content-sha256", AssinaturaS3V4.HashDoCorpoVazio);
        foreach ((string nome, string valor) in cabecalhos)
        {
            // Sem validação nos dois lados: o valor segue byte a byte como foi assinado.
            if (!requisicao.Headers.TryAddWithoutValidation(nome, valor))
            {
                requisicao.Content.Headers.TryAddWithoutValidation(nome, valor);
            }
        }

        using HttpClient httpClient = _httpClientFactory.CreateClient(nameof(MinioStorageService));
        using HttpResponseMessage resposta = await httpClient.SendAsync(requisicao, cancellationToken).ConfigureAwait(false);
        string corpo = await resposta.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        // O S3 pode responder 200 e trazer o erro no corpo quando a cópia falha depois de começar.
        if (!resposta.IsSuccessStatusCode || corpo.Contains("<Error>", StringComparison.Ordinal))
        {
            throw new HttpRequestException(
                $"A cópia para {bucket}/{chave} foi recusada pelo armazenamento ({(int)resposta.StatusCode}): {corpo}",
                inner: null,
                resposta.StatusCode);
        }
    }

    /// <summary>
    /// <c>Content-Disposition</c> de anexo com o nome em duas formas (RFC 6266): <c>filename*</c> em
    /// UTF-8 codificado (RFC 5987), que todo navegador atual lê, e <c>filename</c> em ASCII para o
    /// cliente que só conhece a forma antiga — com os acentos retirados e o que não couber em ASCII
    /// imprimível trocado por sublinhado.
    /// </summary>
    private static string ContentDispositionDeAnexo(string nome)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nome);

        string composto = nome.Normalize(NormalizationForm.FormC);
        string semAcento = string.Concat(composto
            .Normalize(NormalizationForm.FormD)
            .Where(static c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .Select(static c => c is >= ' ' and <= '~' and not '"' and not '\\' ? c : '_'));

        return $"attachment; filename=\"{semAcento}\"; filename*=UTF-8''{Uri.EscapeDataString(composto)}";
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
