namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Formato do arquivo que o administrador envia ao processo. Catálogo fechado: PDF para o que se
/// publica como está, DOCX e ODT para o que a pessoa pode editar. O modelo de documento aceita só
/// o subconjunto editável. Os valores são gravados; não se reordenam.
/// </summary>
public enum FormatoDeDocumento
{
    Docx = 1,
    Odt = 2,
    Pdf = 3,
}
