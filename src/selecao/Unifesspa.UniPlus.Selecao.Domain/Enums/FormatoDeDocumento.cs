namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Formato do documento que o administrador envia para o processo publicar. Catálogo fechado:
/// PDF para o que se publica como está, DOCX e ODT para o que o leitor pode editar.
/// </summary>
public enum FormatoDeDocumento
{
    Pdf = 1,
    Docx = 2,
    Odt = 3,
}
