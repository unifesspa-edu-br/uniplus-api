namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Forma o texto-base de um código estável a partir de um nome ou de uma identificação digitada: sem acento, em maiúsculas,
/// com cada sequência de caracteres que não são letra ASCII nem dígito trocada por um único sublinhado e sem sublinhado nas
/// pontas. Textos que só diferem em acento, caixa ou pontuação geram o mesmo resultado.
/// </summary>
internal static partial class GeradorDeCodigoEstavel
{
    public static string Normalizar(string texto)
    {
        ArgumentNullException.ThrowIfNull(texto);

        string maiusculasSemAcento = new([.. SemMarcasDeAcento(texto)]);
        return SequenciaForaDoCodigo().Replace(maiusculasSemAcento, "_").Trim('_');
    }

    private static IEnumerable<char> SemMarcasDeAcento(string texto) =>
        texto.Normalize(NormalizationForm.FormKD)
            .Where(static c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .Select(static c => char.ToUpperInvariant(c));

    [GeneratedRegex("[^A-Z0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex SequenciaForaDoCodigo();
}
