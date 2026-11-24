namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Formato de arquivo aceito para apresentação de documento.</summary>
public enum FormatoPermitido
{
    [CodigoDeContrato("PDF", "PDF", Descricao = "Documento em formato PDF.")]
    Pdf = 1,
    [CodigoDeContrato("JPEG", "JPEG", Descricao = "Imagem em formato JPEG.")]
    Jpeg = 2,
    [CodigoDeContrato("PNG", "PNG", Descricao = "Imagem em formato PNG.")]
    Png = 3,
}
