namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Extensions;

/// <summary>
/// Texto que entra numa cópia congelada do processo: aparado e em NFC, a forma com que o
/// envelope canônico o grava (<see cref="HashCanonicalComputer.NormalizeNfc"/>). Sem a
/// normalização na fronteira do congelamento, o mesmo texto em forma decomposta vira outro valor
/// depois de um ciclo de retificação. O texto também tem de caber na coluna que o recebe, sem o
/// caractere nulo, que o Postgres recusa com erro de banco.
/// </summary>
public static class TextoCongelado
{
    private const char CaractereNulo = (char)0;

    /// <summary>
    /// O texto aparado e em NFC, ou <see langword="null"/> quando ele não pode ser normalizado —
    /// traz um não-caractere ou um surrogate sem par.
    /// </summary>
    public static string? Normalizar(string texto)
    {
        ArgumentNullException.ThrowIfNull(texto);

        return TextoNormalizavel.TentarNormalizar(texto.Trim(), out string normalizado) ? normalizado : null;
    }

    /// <summary>
    /// O texto aparado e em NFC, ou <see langword="null"/> quando ausente, em branco ou impossível
    /// de normalizar.
    /// </summary>
    public static string? NormalizarOuNulo(string? texto) =>
        string.IsNullOrWhiteSpace(texto) ? null : Normalizar(texto);

    /// <summary>O texto traz o caractere nulo (U+0000).</summary>
    public static bool ContemCaractereNulo(string texto)
    {
        ArgumentNullException.ThrowIfNull(texto);

        return texto.Contains(CaractereNulo);
    }

    /// <summary>O texto cabe na coluna de <paramref name="maxLength"/> caracteres e não traz o caractere nulo.</summary>
    public static bool CabeNaColuna(string texto, int maxLength) =>
        !ContemCaractereNulo(texto) && texto.Length <= maxLength;
}
