namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Formato do arquivo que o administrador envia ao processo. Catálogo fechado: PDF para o que se
/// publica como está, DOCX e ODT para o que a pessoa pode editar. O modelo de documento aceita só
/// o subconjunto editável. Os valores são gravados; não se reordenam.
/// </summary>
public enum FormatoDeDocumento
{
    [CodigoDeContrato("DOCX", "DOCX", Descricao = "Documento de texto em formato DOCX.")]
    Docx = 1,
    [CodigoDeContrato("ODT", "ODT", Descricao = "Documento de texto em formato ODT.")]
    Odt = 2,
    [CodigoDeContrato("PDF", "PDF", Descricao = "Documento em formato PDF.")]
    Pdf = 3,
}
