namespace Unifesspa.UniPlus.Infrastructure.Core.Idempotency;

using System.Buffers;
using System.Security.Cryptography;

using Microsoft.AspNetCore.Http;

/// <summary>
/// Leitura do corpo da requisição para o filtro de idempotência: o corpo é lido por inteiro e devolvido ao início, para o
/// model binder consumi-lo depois.
/// </summary>
internal static class RequestBodyReader
{
    private const int BlockSize = 81920;

    /// <exception cref="BadHttpRequestException">Com 413, quando o corpo passa de <paramref name="limit"/>.</exception>
    public static async Task<byte[]> ReadWithoutConsumingAsync(HttpRequest request, long limit, CancellationToken cancellationToken)
    {
        request.EnableBuffering();
        request.Body.Position = 0;

        long declaredSize = request.ContentLength ?? -1;
        if (declaredSize > limit)
        {
            throw BodyTooLarge(limit);
        }

        byte[] body = declaredSize == 0 ? [] : await ReadInBlocksAsync(request.Body, limit, cancellationToken).ConfigureAwait(false);
        request.Body.Position = 0;
        return body;
    }

    public static string Sha256Hex(byte[] body)
    {
        Span<byte> bodyHash = stackalloc byte[32];
        SHA256.HashData(body, bodyHash);
        return Convert.ToHexStringLower(bodyHash);
    }

    // Em blocos e parando no limite: um cliente que envia em chunked, sem Content-Length, não consegue estourar a memória.
    private static async Task<byte[]> ReadInBlocksAsync(Stream body, long limit, CancellationToken cancellationToken)
    {
        byte[] block = ArrayPool<byte>.Shared.Rent(BlockSize);
        try
        {
            using MemoryStream accumulated = new();
            await AccumulateUpToLimitAsync(body, accumulated, block, limit, cancellationToken).ConfigureAwait(false);
            return accumulated.ToArray();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(block);
        }
    }

    private static async Task AccumulateUpToLimitAsync(
        Stream body, MemoryStream accumulated, byte[] block, long limit, CancellationToken cancellationToken)
    {
        int read;
        while ((read = await body.ReadAsync(block.AsMemory(0, BlockSize), cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (accumulated.Length + read > limit)
            {
                body.Position = 0;
                throw BodyTooLarge(limit);
            }

            await accumulated.WriteAsync(block.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }
    }

    private static BadHttpRequestException BodyTooLarge(long limit) =>
        new($"Request body excede o limite de {limit} bytes para endpoints idempotentes.", StatusCodes.Status413PayloadTooLarge);
}
