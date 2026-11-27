namespace Unifesspa.UniPlus.Selecao.API.Http;

using System.Text;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos;

/// <summary>
/// Lê o corpo da requisição que é o documento da configuração: texto UTF-8. O tamanho é limitado pelo servidor, com o
/// <c>[RequestSizeLimit]</c> da operação, e a leitura que passa do limite lança a exceção que vira a recusa 413. O que não é
/// UTF-8 válido é recusado como forma inválida em vez de ser lido com caracteres trocados.
/// </summary>
internal static class CorpoDoDocumento
{
    private const int TamanhoDoBloco = 81920;

    private static readonly UTF8Encoding Utf8Estrito = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static async Task<Result<string>> LerAsync(Stream corpo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(corpo);

        using MemoryStream acumulado = new();
        await corpo.CopyToAsync(acumulado, TamanhoDoBloco, cancellationToken).ConfigureAwait(false);
        return Decodificar(acumulado);
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
