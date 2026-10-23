namespace Unifesspa.UniPlus.Spikes.CifraNoMinio;

using System.Diagnostics;
using System.Globalization;

using Minio;
using Minio.DataModel;
using Minio.DataModel.Args;
using Minio.DataModel.Tags;
using Minio.Exceptions;

/// <summary>
/// O verificador como consumidor de fila. Mesmo trabalho do verificador por webhook (lê o objeto em
/// stream, já decifrado pelo MinIO, passa ao clamd, calcula o SHA-256 e promove o limpo), com as
/// diferenças que a fila exige:
/// <list type="bullet">
/// <item>O pedido traz a chave e o ETag que o PUT devolveu ao candidato, confirmado por ele. Só essa
/// versão é lida, verificada e copiada (as três com o mesmo ETag): um PUT na mesma chave depois da
/// confirmação não é verificado nem promovido, e o arquivo termina em reenvio necessário. A promoção
/// por etiqueta não tem essa garantia na etiquetagem, mais um motivo para ela ter sido recusada.</item>
/// <item>A mesma mensagem pode chegar de novo (o verificador caiu depois de promover e antes de
/// confirmar). A promoção grava no objeto definitivo o SHA-256 e o ETag do envio, e a reentrega olha
/// primeiro o bucket definitivo: o arquivo já promovido devolve o mesmo veredicto e nunca é
/// sobrescrito. O arquivo infectado não é removido: fica na quarentena, que ninguém além do
/// verificador lê, até expirar; assim a reentrega o verifica de novo e devolve INFECTADO.</item>
/// <item>A URL pré-assinada de PUT não limita o tamanho. O arquivo acima do máximo é recusado sem ser
/// lido, antes de chegar ao limite de stream do clamd, que o deixaria sem veredicto.</item>
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
    private const string MetadadoDoSha256 = "sha256";
    private const string MetadadoDoEtagDoEnvio = "etag-envio";

    private static readonly bool PorEtiqueta =
        string.Equals(Environment.GetEnvironmentVariable("PROMOCAO"), "etiqueta", StringComparison.Ordinal);

    // Abaixo do StreamMaxLength padrão do clamd 1.4 (104857600 bytes).
    private static readonly long TamanhoMaximo = Environment.GetEnvironmentVariable("TAMANHO_MAXIMO_BYTES") is { Length: > 0 } valor
        ? long.Parse(valor, CultureInfo.InvariantCulture)
        : 100_000_000;

    public async Task<ArquivoVerificado> Handle(VerificarArquivo pedido, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pedido);

        DateTimeOffset inicio = DateTimeOffset.UtcNow;
        Stopwatch cronometro = Stopwatch.StartNew();

        if (!PorEtiqueta && await StatAsync(config.BucketArquivos, pedido.Chave, cancellationToken) is { } promovido)
        {
            return JaPromovido(pedido, promovido, inicio, cronometro);
        }

        ObjectStat? envio = await StatAsync(config.BucketEnvios, pedido.Chave, cancellationToken);
        if (envio is null || !MesmoETag(envio.ETag, pedido.ETag))
        {
            // Outra entrega do mesmo pedido pode ter promovido o arquivo entre as duas consultas.
            return !PorEtiqueta && await StatAsync(config.BucketArquivos, pedido.Chave, cancellationToken) is { } recemPromovido
                ? JaPromovido(pedido, recemPromovido, inicio, cronometro)
                : Resultado(pedido.Chave, "NAO_ENCONTRADO", string.Empty, 0, inicio, cronometro);
        }

        if (envio.Size > TamanhoMaximo)
        {
            return Resultado(pedido.Chave, "FORA_DO_LIMITE", string.Empty, envio.Size, inicio, cronometro);
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
                .WithMatchETag(pedido.ETag)
                .WithCallbackStream(async (stream, ct) => resultado = await clamd.VerificarAsync(stream, ct)),
            cancellationToken);

        switch (resultado.Veredicto)
        {
            case Veredicto.Limpo:
                await PromoverAsync(pedido, envio.ContentType, resultado.Sha256, cancellationToken);
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

    private async Task PromoverAsync(VerificarArquivo pedido, string contentType, string sha256, CancellationToken cancellationToken)
    {
        if (PorEtiqueta)
        {
            await minio.SetObjectTagsAsync(
                new SetObjectTagsArgs()
                    .WithBucket(config.BucketEnvios)
                    .WithObject(pedido.Chave)
                    .WithTagging(Tagging.GetObjectTags(new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        [EtiquetaDeVerificacao] = Aprovado,
                        [EtiquetaDoSha256] = sha256,
                    })),
                cancellationToken);
            return;
        }

        // REPLACE porque o SHA-256 e o ETag do envio entram no metadado; o Content-Type do PUT é
        // repetido, senão o objeto definitivo perde o tipo e a leitura inline deixa de abrir o PDF.
        await minio.CopyObjectAsync(
            new CopyObjectArgs()
                .WithBucket(config.BucketArquivos)
                .WithObject(pedido.Chave)
                .WithReplaceMetadataDirective(true)
                .WithHeaders(new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["Content-Type"] = contentType,
                    [$"x-amz-meta-{MetadadoDoSha256}"] = sha256,
                    [$"x-amz-meta-{MetadadoDoEtagDoEnvio}"] = SemAspas(pedido.ETag),
                })
                .WithCopyObjectSource(new CopySourceObjectArgs()
                    .WithBucket(config.BucketEnvios)
                    .WithObject(pedido.Chave)
                    .WithMatchETag(pedido.ETag)),
            cancellationToken);

        // O que restar na chave depois da cópia não é a versão confirmada e é descartado com ela.
        await minio.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(config.BucketEnvios).WithObject(pedido.Chave), cancellationToken);
    }

    /// <summary>
    /// A chave já está no bucket definitivo: uma entrega anterior desta mesma mensagem a promoveu. O
    /// veredicto se repete se for a versão confirmada; o objeto definitivo nunca é sobrescrito.
    /// </summary>
    private static ArquivoVerificado JaPromovido(VerificarArquivo pedido, ObjectStat promovido, DateTimeOffset inicio, Stopwatch cronometro) =>
        MesmoETag(Metadado(promovido, MetadadoDoEtagDoEnvio), pedido.ETag)
            ? Resultado(pedido.Chave, "LIMPO", Metadado(promovido, MetadadoDoSha256), promovido.Size, inicio, cronometro)
            : Resultado(pedido.Chave, "NAO_ENCONTRADO", string.Empty, 0, inicio, cronometro);

    // O SDK pode devolver o metadado com ou sem o prefixo x-amz-meta-; o checksum do PUT
    // (x-amz-checksum-sha256) não pode ser confundido com ele.
    private static string Metadado(ObjectStat objeto, string nome) =>
        objeto.MetaData
            .FirstOrDefault(m => m.Key.Equals($"x-amz-meta-{nome}", StringComparison.OrdinalIgnoreCase)
                || m.Key.Equals(nome, StringComparison.OrdinalIgnoreCase)).Value ?? string.Empty;

    private static bool MesmoETag(string? a, string? b) =>
        SemAspas(a).Length > 0 && string.Equals(SemAspas(a), SemAspas(b), StringComparison.OrdinalIgnoreCase);

    private static string SemAspas(string? etag) => (etag ?? string.Empty).Trim('"');

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
