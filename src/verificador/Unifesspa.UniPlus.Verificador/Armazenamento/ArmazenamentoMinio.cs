namespace Unifesspa.UniPlus.Verificador.Armazenamento;

using Microsoft.Extensions.Options;

using Minio;
using Minio.DataModel;
using Minio.DataModel.Args;
using Minio.Exceptions;

using Unifesspa.UniPlus.Verificador.Configuracao;

/// <summary>
/// O armazenamento sobre o MinIO. A leitura, a cópia e a remoção exigem o ETag do pedido: o que o
/// candidato confirmou é exatamente o que é verificado, promovido e removido.
/// </summary>
public sealed class ArmazenamentoMinio : IArmazenamentoDeArquivos
{
    private const string MetadadoDoSha256 = "sha256";
    private const string MetadadoDoETagDoEnvio = "etag-envio";

    private readonly IMinioClient _minio;
    private readonly ArmazenamentoOptions _options;

    public ArmazenamentoMinio(IMinioClient minio, IOptions<VerificadorOptions> options)
    {
        ArgumentNullException.ThrowIfNull(minio);
        ArgumentNullException.ThrowIfNull(options);

        _minio = minio;
        _options = options.Value.Armazenamento;
    }

    public Task<ObjetoArmazenado?> ConsultarDefinitivoAsync(string chave, CancellationToken cancellationToken) =>
        ConsultarAsync(_options.BucketArquivos, chave, cancellationToken);

    public Task<ObjetoArmazenado?> ConsultarEnvioAsync(string chave, CancellationToken cancellationToken) =>
        ConsultarAsync(_options.BucketEnvios, chave, cancellationToken);

    public async Task<bool> LerEnvioAsync(string chave, string etag, Stream destino, CancellationToken cancellationToken)
    {
        try
        {
            await _minio.GetObjectAsync(ArgumentosDaLeitura(chave, etag, destino), cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (VersaoAusente(ex))
        {
            return false;
        }
        catch (Exception ex) when (EhFalhaDoArmazenamento(ex, cancellationToken))
        {
            throw FalhaNoArmazenamento("ler o envio no armazenamento", ex);
        }
    }

    public async Task PromoverAsync(
        string chaveDeEnvio,
        string etag,
        string chaveDefinitiva,
        string contentType,
        string sha256,
        CancellationToken cancellationToken)
    {
        try
        {
            await _minio.CopyObjectAsync(
                ArgumentosDaPromocao(chaveDeEnvio, etag, chaveDefinitiva, contentType, sha256),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (EhFalhaDoArmazenamento(ex, cancellationToken))
        {
            throw FalhaNoArmazenamento("promover o arquivo no armazenamento", ex);
        }
    }

    public async Task RemoverEnvioAsync(string chave, string etag, CancellationToken cancellationToken)
    {
        if (!await EnvioDaMesmaVersaoAsync(chave, etag, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            await _minio.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(_options.BucketEnvios).WithObject(chave), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (EhFalhaDoArmazenamento(ex, cancellationToken))
        {
            throw FalhaNoArmazenamento("remover o envio no armazenamento", ex);
        }
    }

    /// <summary>
    /// A falha do MinIO, da rede ou do prazo do SDK. O cancelamento pedido pelo chamador não é falha
    /// do armazenamento e segue como cancelamento.
    /// </summary>
    public static bool EhFalhaDoArmazenamento(Exception ex, CancellationToken cancellationToken) =>
        ex is MinioException or HttpRequestException or IOException
        || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested);

    public static bool MesmoETag(string? a, string? b) =>
        SemAspas(a).Length > 0 && string.Equals(SemAspas(a), SemAspas(b), StringComparison.OrdinalIgnoreCase);

    private async Task<ObjetoArmazenado?> ConsultarAsync(string bucket, string chave, CancellationToken cancellationToken)
    {
        try
        {
            return Descrever(await _minio.StatObjectAsync(
                new StatObjectArgs().WithBucket(bucket).WithObject(chave),
                cancellationToken).ConfigureAwait(false));
        }
        catch (Exception ex) when (ObjetoAusente(ex))
        {
            return null;
        }
        catch (Exception ex) when (EhFalhaDoArmazenamento(ex, cancellationToken))
        {
            throw FalhaNoArmazenamento("consultar o armazenamento", ex);
        }
    }

    private static ObjetoArmazenado Descrever(ObjectStat objeto) =>
        new(objeto.ETag, objeto.Size, Metadado(objeto, MetadadoDoSha256), Metadado(objeto, MetadadoDoETagDoEnvio));

    /// <summary>
    /// O MinIO não aplica If-Match à remoção; confere-se o ETag logo antes. Um PUT na janela entre a
    /// conferência e a remoção apaga uma versão que nunca seria promovida.
    /// </summary>
    private async Task<bool> EnvioDaMesmaVersaoAsync(string chave, string etag, CancellationToken cancellationToken)
    {
        ObjetoArmazenado? atual = await ConsultarEnvioAsync(chave, cancellationToken).ConfigureAwait(false);
        return atual is not null && MesmoETag(atual.ETag, etag);
    }

    private GetObjectArgs ArgumentosDaLeitura(string chave, string etag, Stream destino) =>
        new GetObjectArgs()
            .WithBucket(_options.BucketEnvios)
            .WithObject(chave)
            .WithMatchETag(etag)
            .WithCallbackStream((origem, ct) => origem.CopyToAsync(destino, ct));

    /// <summary>
    /// O S3 não define If-None-Match na cópia. Equivale a consulta prévia ao definitivo mais o
    /// conteúdo da cópia depender só do ETag de origem: duas cópias concorrentes gravam o mesmo objeto.
    /// REPLACE porque o SHA-256 e o ETag do envio entram no metadado, e o Content-Type precisa ser repetido.
    /// </summary>
    private CopyObjectArgs ArgumentosDaPromocao(string chaveDeEnvio, string etag, string chaveDefinitiva, string contentType, string sha256) =>
        new CopyObjectArgs()
            .WithBucket(_options.BucketArquivos)
            .WithObject(chaveDefinitiva)
            .WithReplaceMetadataDirective(true)
            .WithHeaders(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Content-Type"] = contentType,
                [$"x-amz-meta-{MetadadoDoSha256}"] = sha256,
                [$"x-amz-meta-{MetadadoDoETagDoEnvio}"] = SemAspas(etag),
            })
            .WithCopyObjectSource(new CopySourceObjectArgs()
                .WithBucket(_options.BucketEnvios)
                .WithObject(chaveDeEnvio)
                .WithMatchETag(etag));

    private static ArmazenamentoException FalhaNoArmazenamento(string acao, Exception ex) =>
        new($"Falha ao {acao} ({ex.GetType().Name}).", ex);

    public static bool ObjetoAusente(Exception ex) =>
        ex is ObjectNotFoundException or ErrorResponseException { Response.Code: "NoSuchKey" };

    private static bool VersaoAusente(Exception ex) =>
        ObjetoAusente(ex) || ex is ErrorResponseException { Response.Code: "PreconditionFailed" };

    // O SDK pode devolver o metadado com ou sem o prefixo x-amz-meta-; o checksum do PUT
    // (x-amz-checksum-sha256) não pode ser confundido com ele.
    private static string? Metadado(ObjectStat objeto, string nome) =>
        objeto.MetaData
            .FirstOrDefault(m => m.Key.Equals($"x-amz-meta-{nome}", StringComparison.OrdinalIgnoreCase)
                || m.Key.Equals(nome, StringComparison.OrdinalIgnoreCase)).Value;

    private static string SemAspas(string? etag) => (etag ?? string.Empty).Trim('"');
}
