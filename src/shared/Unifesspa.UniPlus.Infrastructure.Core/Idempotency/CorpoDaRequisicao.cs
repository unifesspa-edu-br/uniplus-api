namespace Unifesspa.UniPlus.Infrastructure.Core.Idempotency;

using System.Buffers;
using System.Security.Cryptography;

using Microsoft.AspNetCore.Http;

/// <summary>
/// Leitura do corpo da requisição para o filtro de idempotência: o corpo é lido por inteiro e devolvido ao início, para o
/// model binder consumi-lo depois.
/// </summary>
internal static class CorpoDaRequisicao
{
    private const int TamanhoDoBloco = 81920;

    /// <exception cref="BadHttpRequestException">Com 413, quando o corpo passa de <paramref name="limite"/>.</exception>
    public static async Task<byte[]> LerSemConsumirAsync(HttpRequest request, long limite, CancellationToken cancellationToken)
    {
        request.EnableBuffering();
        request.Body.Position = 0;

        long tamanhoDeclarado = request.ContentLength ?? -1;
        if (tamanhoDeclarado > limite)
        {
            throw CorpoGrandeDemais(limite);
        }

        byte[] corpo = tamanhoDeclarado == 0 ? [] : await LerEmBlocosAsync(request.Body, limite, cancellationToken).ConfigureAwait(false);
        request.Body.Position = 0;
        return corpo;
    }

    public static string ResumoSha256(byte[] corpo)
    {
        Span<byte> resumo = stackalloc byte[32];
        SHA256.HashData(corpo, resumo);
        return Convert.ToHexStringLower(resumo);
    }

    // Em blocos e parando no limite: um cliente que envia em chunked, sem Content-Length, não consegue estourar a memória.
    private static async Task<byte[]> LerEmBlocosAsync(Stream corpo, long limite, CancellationToken cancellationToken)
    {
        byte[] bloco = ArrayPool<byte>.Shared.Rent(TamanhoDoBloco);
        try
        {
            using MemoryStream acumulado = new();
            await AcumularAteOLimiteAsync(corpo, acumulado, bloco, limite, cancellationToken).ConfigureAwait(false);
            return acumulado.ToArray();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(bloco);
        }
    }

    private static async Task AcumularAteOLimiteAsync(
        Stream corpo, MemoryStream acumulado, byte[] bloco, long limite, CancellationToken cancellationToken)
    {
        int lidos;
        while ((lidos = await corpo.ReadAsync(bloco.AsMemory(0, TamanhoDoBloco), cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (acumulado.Length + lidos > limite)
            {
                corpo.Position = 0;
                throw CorpoGrandeDemais(limite);
            }

            await acumulado.WriteAsync(bloco.AsMemory(0, lidos), cancellationToken).ConfigureAwait(false);
        }
    }

    private static BadHttpRequestException CorpoGrandeDemais(long limite) =>
        new($"Request body excede o limite de {limite} bytes para endpoints idempotentes.", StatusCodes.Status413PayloadTooLarge);
}
