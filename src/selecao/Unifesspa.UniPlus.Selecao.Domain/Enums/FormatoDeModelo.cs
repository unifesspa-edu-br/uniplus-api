namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Formato editável do modelo de documento que a exigência oferece ao candidato: o arquivo que ele
/// baixa, preenche, assina e devolve. Catálogo fechado — PDF e imagem não são editáveis.
/// </summary>
public enum FormatoDeModelo
{
    Nenhum = 0,
    Docx = 1,
    Odt = 2,
}
