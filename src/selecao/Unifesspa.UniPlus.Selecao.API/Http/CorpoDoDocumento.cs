namespace Unifesspa.UniPlus.Selecao.API.Http;

using System.Buffers;
using System.Text;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos;

/// <summary>
/// Lê o corpo da requisição que é o documento da configuração: texto UTF-8 de até o tamanho máximo do formato. A leitura para
/// no primeiro byte além do teto, sem guardar mais do que o teto, e o que não é UTF-8 válido é recusado como forma inválida em
/// vez de ser lido com caracteres trocados.
/// </summary>
internal static class CorpoDoDocumento
{
    private const int TamanhoDoBloco = 81920;

    private static readonly UTF8Encoding Utf8Estrito = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static async Task<Result<string>> LerAsync(Stream corpo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(corpo);

        using MemoryStream acumulado = new();
        byte[] bloco = ArrayPool<byte>.Shared.Rent(TamanhoDoBloco);
        try
        {
            return await AcumularAteOTetoAsync(corpo, acumulado, bloco, cancellationToken).ConfigureAwait(false)
                ? Result<string>.ValidationFailure([CausasDaForma.TamanhoAcimaDoLimite()])
                : Decodificar(acumulado);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(bloco);
        }
    }

    // Verdadeiro quando o corpo passa do teto: a leitura para no bloco que o ultrapassa.
    private static async Task<bool> AcumularAteOTetoAsync(Stream corpo, MemoryStream acumulado, byte[] bloco, CancellationToken cancellationToken)
    {
        int lidos;
        while ((lidos = await corpo.ReadAsync(bloco.AsMemory(0, TamanhoDoBloco), cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (acumulado.Length + lidos > LimitesDoDocumento.TamanhoMaximoEmBytes)
            {
                return true;
            }

            await acumulado.WriteAsync(bloco.AsMemory(0, lidos), cancellationToken).ConfigureAwait(false);
        }

        return false;
    }

    private static Result<string> Decodificar(MemoryStream acumulado)
    {
        try
        {
            return Result<string>.Success(Utf8Estrito.GetString(acumulado.GetBuffer(), 0, (int)acumulado.Length));
        }
        catch (DecoderFallbackException)
        {
            return Result<string>.ValidationFailure(RecusaDaForma.ComGrupo([CausasDaForma.Malformado()]));
        }
    }
}
