namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using Enums;

/// <summary>
/// O que cada finalidade aceita de formato: o edital é publicado como está, em PDF; o que o
/// acompanha pode ser PDF, DOCX ou ODT.
/// </summary>
public static class FinalidadesDeDocumento
{
    /// <summary>A finalidade existe no catálogo.</summary>
    public static bool Conhecida(FinalidadeDoDocumento finalidade) => Enum.IsDefined(finalidade);

    /// <summary>O formato é aceito para a finalidade.</summary>
    public static bool AceitaFormato(FinalidadeDoDocumento finalidade, FormatoDeDocumento formato) => finalidade switch
    {
        FinalidadeDoDocumento.DocumentoDoEdital => formato == FormatoDeDocumento.Pdf,
        FinalidadeDoDocumento.AnexoDoEdital or FinalidadeDoDocumento.DocumentoDeAto => true,
        _ => false,
    };
}
