namespace Unifesspa.UniPlus.Verificador.Verificacao;

using UglyToad.PdfPig;
using UglyToad.PdfPig.Exceptions;

using Unifesspa.UniPlus.Verificador.Contratos;

/// <summary>As conferências do conteúdo que não dependem do antivírus.</summary>
public static class ConferenciaDoArquivo
{
    /// <summary>Quantos bytes do início bastam para reconhecer os três formatos.</summary>
    public const int BytesDeAssinatura = 8;

    private static readonly byte[] AssinaturaPdf = "%PDF-"u8.ToArray();
    private static readonly byte[] AssinaturaJpeg = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] AssinaturaPng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>O conteúdo começa com a assinatura do formato declarado.</summary>
    public static bool FormatoCorresponde(FormatoDoArquivo declarado, ReadOnlySpan<byte> inicio) =>
        declarado switch
        {
            FormatoDoArquivo.Pdf => inicio.StartsWith(AssinaturaPdf),
            FormatoDoArquivo.Jpeg => inicio.StartsWith(AssinaturaJpeg),
            FormatoDoArquivo.Png => inicio.StartsWith(AssinaturaPng),
            _ => false,
        };

    /// <summary>
    /// O PDF exige senha para abrir. A senha vazia é tentada: o PDF protegido só contra edição ou
    /// impressão abre com ela e segue. O PDF que a PdfPig não consegue ler por outro motivo não é
    /// recusado aqui; quem decide sobre ele é o antivírus.
    /// </summary>
    public static bool PdfExigeSenhaParaAbrir(Stream pdf)
    {
        ArgumentNullException.ThrowIfNull(pdf);

        try
        {
            using PdfDocument documento = PdfDocument.Open(pdf, new ParsingOptions { Passwords = [string.Empty] });
            return false;
        }
        catch (PdfDocumentEncryptedException)
        {
            return true;
        }
        catch (Exception ex) when (ex is not (OperationCanceledException or OutOfMemoryException))
        {
            return false;
        }
    }
}
