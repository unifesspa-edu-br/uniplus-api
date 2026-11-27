namespace Unifesspa.UniPlus.Infrastructure.Core.Idempotency;

using System.Security.Cryptography;

using Microsoft.AspNetCore.Http;

/// <summary>
/// Leitura do corpo da requisição para o filtro de idempotência: o corpo é lido por inteiro e devolvido ao início, para o
/// model binder consumi-lo depois.
/// </summary>
/// <remarks>
/// A leitura não impõe tamanho: o limite do corpo é do servidor, aplicado pelo <c>LimiteDoCorpoDaRequisicaoMiddleware</c>, e a
/// exceção com 413 que a leitura lança ao passar dele sobe até a tradução geral de erros.
/// </remarks>
internal static class RequestBodyReader
{
    private const int BlockSize = 81920;

    public static async Task<byte[]> ReadWithoutConsumingAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        request.EnableBuffering();
        request.Body.Position = 0;

        byte[] body = request.ContentLength == 0 ? [] : await ReadInBlocksAsync(request.Body, cancellationToken).ConfigureAwait(false);
        request.Body.Position = 0;
        return body;
    }

    public static string Sha256Hex(byte[] body)
    {
        Span<byte> bodyHash = stackalloc byte[32];
        SHA256.HashData(body, bodyHash);
        return Convert.ToHexStringLower(bodyHash);
    }

    private static async Task<byte[]> ReadInBlocksAsync(Stream body, CancellationToken cancellationToken)
    {
        using MemoryStream accumulated = new();
        await body.CopyToAsync(accumulated, BlockSize, cancellationToken).ConfigureAwait(false);
        return accumulated.ToArray();
    }
}
