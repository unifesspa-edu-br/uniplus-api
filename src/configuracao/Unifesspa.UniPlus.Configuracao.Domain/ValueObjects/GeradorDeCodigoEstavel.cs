namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using System.Globalization;
using System.Text;

/// <summary>
/// Forma o texto-base de um código estável a partir de um nome ou de uma identificação digitada: sem acento, em maiúsculas,
/// com cada sequência de caracteres que não são letra ASCII nem dígito trocada por um único sublinhado e sem sublinhado nas
/// pontas. Textos que só diferem em acento, caixa ou pontuação geram o mesmo resultado.
/// </summary>
internal static class GeradorDeCodigoEstavel
{
    public static string Normalizar(string texto)
    {
        ArgumentNullException.ThrowIfNull(texto);

        StringBuilder codigo = new(texto.Length);
        bool separadorPendente = false;

        foreach (char c in SemMarcasDeAcento(texto))
        {
            if (char.IsAsciiLetterUpper(c) || char.IsAsciiDigit(c))
            {
                AcrescentarComSeparador(codigo, c, ref separadorPendente);
            }
            else
            {
                separadorPendente = true;
            }
        }

        return codigo.ToString();
    }

    private static void AcrescentarComSeparador(StringBuilder codigo, char c, ref bool separadorPendente)
    {
        if (separadorPendente && codigo.Length > 0)
        {
            codigo.Append('_');
        }

        separadorPendente = false;
        codigo.Append(c);
    }

    private static IEnumerable<char> SemMarcasDeAcento(string texto) =>
        texto.Normalize(NormalizationForm.FormKD)
            .Where(static c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .Select(static c => char.ToUpperInvariant(c));
}
