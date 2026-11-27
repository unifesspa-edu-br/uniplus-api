namespace Unifesspa.UniPlus.Infrastructure.Core.Pagination;

using System.Globalization;

/// <summary>
/// Formato do instante que viaja no cursor: ISO-8601 em UTC, de largura fixa, cuja ordem textual coincide com a
/// cronológica.
/// </summary>
public static class InstanteDoCursor
{
    public static string Escrever(DateTimeOffset instante) =>
        instante.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    /// <exception cref="CursorAnchorMismatchException">O texto não é um instante escrito por <see cref="Escrever"/>.</exception>
    public static DateTimeOffset Ler(string texto) =>
        TentarLer(texto, out DateTimeOffset instante)
            ? instante
            : throw new CursorAnchorMismatchException("O instante do cursor não pôde ser lido.");

    public static bool TentarLer(string? texto, out DateTimeOffset instante) =>
        DateTimeOffset.TryParseExact(
            texto,
            "O",
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out instante);
}
