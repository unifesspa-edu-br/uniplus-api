namespace Unifesspa.UniPlus.Spikes.CifraNoMinio;

using System.Diagnostics;

using Minio;
using Minio.DataModel;
using Minio.DataModel.Args;
using Minio.DataModel.Tags;
using Minio.Exceptions;

/// <summary>
/// O verificador como consumidor de fila. Mesmo trabalho do verificador por webhook (lê o objeto em
/// stream, já decifrado pelo MinIO, passa ao clamd, calcula o SHA-256 e promove o limpo), com duas
/// diferenças que a fila exige:
/// <list type="bullet">
/// <item>O pedido traz só a chave. O verificador lê o ETag atual e exige o mesmo ETag na leitura e
/// na cópia: o que foi verificado é o que é promovido. A promoção por etiqueta não tem essa garantia
/// (a etiqueta não aceita condição de ETag), mais um motivo para ela ter sido recusada.</item>
/// <item>A mesma mensagem pode chegar de novo (o verificador caiu depois de promover e antes de
/// confirmar). Por isso a promoção grava o SHA-256 junto do objeto, e a reentrega de um arquivo já
/// promovido devolve o mesmo veredicto sem verificar outra vez. O arquivo infectado não é removido:
/// fica na quarentena, que ninguém além do verificador lê, até expirar; assim a reentrega o
/// verifica de novo e devolve INFECTADO, e não NAO_ENCONTRADO.</item>
/// </list>
/// <c>PROMOCAO</c> escolhe como o arquivo limpo é liberado: <c>copia</c> para o bucket definitivo
/// (o desenho da ADR-0146) ou <c>etiqueta</c> no próprio bucket de envio, que a política da API
/// exige para assinar a leitura.
/// </summary>
public sealed partial class VerificadorDaFila(Configuracao config, IMinioClient minio, Clamd clamd, ILogger<VerificadorDaFila> logger)
{
    public const string EtiquetaDeVerificacao = "verificacao";
    public const string Aprovado = "aprovado";
    private const string EtiquetaDoSha256 = "sha256";
    private const string MetadadoDoSha256 = "x-amz-meta-sha256";

    private static readonly bool PorEtiqueta =
        string.Equals(Environment.GetEnvironmentVariable("PROMOCAO"), "etiqueta", StringComparison.Ordinal);

    public async Task<ArquivoVerificado> Handle(VerificarArquivo pedido, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pedido);

        DateTimeOffset inicio = DateTimeOffset.UtcNow;
        Stopwatch cronometro = Stopwatch.StartNew();

        ObjectStat? envio = await StatAsync(config.BucketEnvios, pedido.Chave, cancellationToken);
        if (envio is null)
        {
            return await JaPromovidoAsync(pedido.Chave, inicio, cronometro, cancellationToken);
        }

        if (PorEtiqueta && await EtiquetasAsync(pedido.Chave, cancellationToken) is { } etiquetas
            && etiquetas.TryGetValue(EtiquetaDeVerificacao, out string? estado) && estado == Aprovado)
        {
            return Resultado(pedido.Chave, "LIMPO", etiquetas.GetValueOrDefault(EtiquetaDoSha256, string.Empty), envio.Size, inicio, cronometro);
        }

        (Veredicto Veredicto, string Sha256, long Bytes) resultado = (Veredicto.Indisponivel, string.Empty, 0);
        await minio.GetObjectAsync(
            new GetObjectArgs()
                .WithBucket(config.BucketEnvios)
                .WithObject(pedido.Chave)
                .WithMatchETag(envio.ETag)
                .WithCallbackStream(async (stream, ct) => resultado = await clamd.VerificarAsync(stream, ct)),
            cancellationToken);

        switch (resultado.Veredicto)
        {
            case Veredicto.Limpo:
                await PromoverAsync(pedido.Chave, envio.ETag, resultado.Sha256, cancellationToken);
                break;
            case Veredicto.Infectado:
                break;
            default:
                // Sem veredicto, a exceção devolve a mensagem para nova tentativa; nunca recusa.
                throw new ClamdIndisponivelException(pedido.Chave);
        }

        ArquivoVerificado verificado = Resultado(
            pedido.Chave, resultado.Veredicto == Veredicto.Limpo ? "LIMPO" : "INFECTADO", resultado.Sha256, resultado.Bytes, inicio, cronometro);
        LogVerificado(logger, pedido.Chave, verificado.Veredicto, verificado.VerificacaoMs);
        return verificado;
    }

    private async Task PromoverAsync(string chave, string etag, string sha256, CancellationToken cancellationToken)
    {
        if (PorEtiqueta)
        {
            await minio.SetObjectTagsAsync(
                new SetObjectTagsArgs()
                    .WithBucket(config.BucketEnvios)
                    .WithObject(chave)
                    .WithTagging(Tagging.GetObjectTags(new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        [EtiquetaDeVerificacao] = Aprovado,
                        [EtiquetaDoSha256] = sha256,
                    })),
                cancellationToken);
            return;
        }

        await minio.CopyObjectAsync(
            new CopyObjectArgs()
                .WithBucket(config.BucketArquivos)
                .WithObject(chave)
                .WithReplaceMetadataDirective(true)
                .WithHeaders(new Dictionary<string, string>(StringComparer.Ordinal) { [MetadadoDoSha256] = sha256 })
                .WithCopyObjectSource(new CopySourceObjectArgs()
                    .WithBucket(config.BucketEnvios)
                    .WithObject(chave)
                    .WithMatchETag(etag)),
            cancellationToken);
        // Só remove a versão que foi copiada: um PUT novo na mesma chave, depois da confirmação, não
        // é apagado aqui; ele não foi confirmado e expira na quarentena.
        if (await StatAsync(config.BucketEnvios, chave, cancellationToken) is { } atual && atual.ETag == etag)
        {
            await minio.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(config.BucketEnvios).WithObject(chave), cancellationToken);
        }
    }

    /// <summary>
    /// O objeto já não está no bucket de envio: ou foi promovido numa entrega anterior desta mesma
    /// mensagem, e o veredicto se repete, ou não chegou ou expirou na quarentena antes de ser
    /// verificado, e o candidato precisa enviar de novo.
    /// </summary>
    private async Task<ArquivoVerificado> JaPromovidoAsync(string chave, DateTimeOffset inicio, Stopwatch cronometro, CancellationToken cancellationToken)
    {
        ObjectStat? promovido = PorEtiqueta ? null : await StatAsync(config.BucketArquivos, chave, cancellationToken);
        if (promovido is null)
        {
            return Resultado(chave, "NAO_ENCONTRADO", string.Empty, 0, inicio, cronometro);
        }

        // O SDK pode devolver o metadado com ou sem o prefixo x-amz-meta-; o checksum do PUT
        // (x-amz-checksum-sha256) não pode ser confundido com ele.
        string sha256 = promovido.MetaData
            .FirstOrDefault(m => m.Key.Equals(MetadadoDoSha256, StringComparison.OrdinalIgnoreCase)
                || m.Key.Equals(EtiquetaDoSha256, StringComparison.OrdinalIgnoreCase)).Value ?? string.Empty;
        return Resultado(chave, "LIMPO", sha256, promovido.Size, inicio, cronometro);
    }

    private async Task<ObjectStat?> StatAsync(string bucket, string chave, CancellationToken cancellationToken)
    {
        try
        {
            return await minio.StatObjectAsync(new StatObjectArgs().WithBucket(bucket).WithObject(chave), cancellationToken);
        }
        catch (ObjectNotFoundException)
        {
            return null;
        }
    }

    private async Task<IReadOnlyDictionary<string, string>?> EtiquetasAsync(string chave, CancellationToken cancellationToken)
    {
        Tagging? etiquetas = await minio.GetObjectTagsAsync(
            new GetObjectTagsArgs().WithBucket(config.BucketEnvios).WithObject(chave), cancellationToken);
        return etiquetas?.Tags is { } tags ? new Dictionary<string, string>(tags, StringComparer.Ordinal) : null;
    }

    private static ArquivoVerificado Resultado(string chave, string veredicto, string sha256, long bytes, DateTimeOffset inicio, Stopwatch cronometro) =>
        new(chave, veredicto, sha256, bytes, inicio, DateTimeOffset.UtcNow, cronometro.Elapsed.TotalMilliseconds, Environment.MachineName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Arquivo {Chave} verificado: {Veredicto} em {Ms} ms")]
    private static partial void LogVerificado(ILogger logger, string chave, string veredicto, double ms);
}
