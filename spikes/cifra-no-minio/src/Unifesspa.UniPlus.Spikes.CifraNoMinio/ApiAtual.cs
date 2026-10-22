namespace Unifesspa.UniPlus.Spikes.CifraNoMinio;

using System.Diagnostics;
using System.Security.Cryptography;

using Minio;
using Minio.DataModel;
using Minio.DataModel.Args;

/// <summary>
/// O fluxo atual da confirmação, nos mesmos passos de <c>ConfirmacaoDoArquivoEnviado</c> e
/// <c>LeituraDoArquivoEnviado</c> da API: stat, leitura do objeto inteiro para um <c>byte[]</c>, clamd
/// sobre o <c>byte[]</c>, SHA-256, cifra AES-GCM no formato do <c>LocalAesEncryptionService</c>
/// (nonce + tag + texto cifrado, num array novo) e gravação da cópia selada; o objeto de envio, em
/// claro, é apagado. Serve só de base de comparação nas medições.
/// </summary>
public static class ApiAtual
{
    private const int TamanhoDoNonce = 12;
    private const int TamanhoDaTag = 16;

    public static void Mapear(WebApplication app, Configuracao config)
    {
        ArgumentNullException.ThrowIfNull(config);

        IMinioClient minio = config.ClienteInterno();
        Clamd clamd = new(config.ClamdHost, config.ClamdPorta);
        byte[] chave = Convert.FromBase64String(Configuracao.Obrigatoria("CHAVE_AES_LOCAL"));

        app.MapPost("/confirmar/{objeto}", async (string objeto, CancellationToken cancellationToken) =>
        {
            Stopwatch cronometro = Stopwatch.StartNew();
            ObjectStat info = await minio.StatObjectAsync(
                new StatObjectArgs().WithBucket(config.BucketEnviosAtual).WithObject(objeto), cancellationToken);

            byte[] conteudo = [];
            await minio.GetObjectAsync(
                new GetObjectArgs()
                    .WithBucket(config.BucketEnviosAtual)
                    .WithObject(objeto)
                    .WithCallbackStream(async (stream, ct) =>
                    {
                        using MemoryStream buffer = new();
                        await stream.CopyToAsync(buffer, ct);
                        conteudo = buffer.ToArray();
                    }),
                cancellationToken);

            Veredicto veredicto = await clamd.VerificarAsync(conteudo, cancellationToken);
            if (veredicto != Veredicto.Limpo)
            {
                return Results.Ok(new { veredicto = veredicto.ToString().ToUpperInvariant(), ms = cronometro.Elapsed.TotalMilliseconds });
            }

            string sha256 = Convert.ToHexStringLower(SHA256.HashData(conteudo));
            byte[] selado = Selar(chave, conteudo);
            using (MemoryStream gravar = new(selado, writable: false))
            {
                await minio.PutObjectAsync(
                    new PutObjectArgs()
                        .WithBucket(config.BucketSelados)
                        .WithObject(objeto)
                        .WithStreamData(gravar)
                        .WithObjectSize(selado.LongLength)
                        .WithContentType("application/octet-stream"),
                    cancellationToken);
            }

            await minio.RemoveObjectAsync(
                new RemoveObjectArgs().WithBucket(config.BucketEnviosAtual).WithObject(objeto), cancellationToken);
            return Results.Ok(new { veredicto = "LIMPO", sha256, bytes = info.Size, ms = cronometro.Elapsed.TotalMilliseconds });
        });
    }

    private static byte[] Selar(byte[] chave, byte[] claro)
    {
        byte[] nonce = new byte[TamanhoDoNonce];
        RandomNumberGenerator.Fill(nonce);
        byte[] cifrado = new byte[claro.Length];
        byte[] tag = new byte[TamanhoDaTag];
        using (AesGcm aes = new(chave, TamanhoDaTag))
        {
            aes.Encrypt(nonce, claro, cifrado, tag);
        }

        byte[] resultado = new byte[TamanhoDoNonce + TamanhoDaTag + cifrado.Length];
        nonce.CopyTo(resultado, 0);
        tag.CopyTo(resultado, TamanhoDoNonce);
        cifrado.CopyTo(resultado, TamanhoDoNonce + TamanhoDaTag);
        return resultado;
    }
}
