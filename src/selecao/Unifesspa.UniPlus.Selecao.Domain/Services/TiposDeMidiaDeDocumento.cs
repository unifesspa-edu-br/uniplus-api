namespace Unifesspa.UniPlus.Selecao.Domain.Services;

/// <summary>
/// Os tipos de mídia dos arquivos que o administrador envia ao processo. Nomeados num só lugar
/// porque o mesmo texto é header assinado da URL de envio, valor conferido na confirmação e tipo
/// do arquivo no acervo.
/// </summary>
public static class TiposDeMidiaDeDocumento
{
    public const string Pdf = "application/pdf";
    public const string Docx = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    public const string Odt = "application/vnd.oasis.opendocument.text";
}
