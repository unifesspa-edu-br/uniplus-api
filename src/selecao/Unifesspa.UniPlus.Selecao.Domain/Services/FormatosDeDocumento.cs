namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using Enums;

/// <summary>
/// A correspondência única entre o formato do arquivo, o tipo de mídia e a extensão, que o modelo
/// de documento e o documento a publicar compartilham.
/// </summary>
public static class FormatosDeDocumento
{
    /// <summary>O formato do tipo de mídia; nulo quando o tipo não é um dos aceitos.</summary>
    public static FormatoDeDocumento? DoTipoDeMidia(string? tipoDeMidia)
    {
        string tipo = (tipoDeMidia ?? string.Empty).Trim();
        if (string.Equals(tipo, TiposDeMidiaDeDocumento.Pdf, StringComparison.OrdinalIgnoreCase))
        {
            return FormatoDeDocumento.Pdf;
        }

        if (string.Equals(tipo, TiposDeMidiaDeDocumento.Docx, StringComparison.OrdinalIgnoreCase))
        {
            return FormatoDeDocumento.Docx;
        }

        return string.Equals(tipo, TiposDeMidiaDeDocumento.Odt, StringComparison.OrdinalIgnoreCase) ? FormatoDeDocumento.Odt : null;
    }

    /// <summary>O tipo de mídia do arquivo no formato.</summary>
    public static string TipoDeMidia(FormatoDeDocumento formato) => formato switch
    {
        FormatoDeDocumento.Pdf => TiposDeMidiaDeDocumento.Pdf,
        FormatoDeDocumento.Docx => TiposDeMidiaDeDocumento.Docx,
        FormatoDeDocumento.Odt => TiposDeMidiaDeDocumento.Odt,
        _ => throw new ArgumentOutOfRangeException(nameof(formato), formato, "Formato de documento sem tipo de mídia."),
    };

    /// <summary>A extensão do arquivo no formato, sem o ponto.</summary>
    public static string Extensao(FormatoDeDocumento formato) => formato switch
    {
        FormatoDeDocumento.Pdf => "pdf",
        FormatoDeDocumento.Docx => "docx",
        FormatoDeDocumento.Odt => "odt",
        _ => throw new ArgumentOutOfRangeException(nameof(formato), formato, "Formato de documento sem extensão."),
    };
}
