namespace Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Readers;

using System.Globalization;
using System.Text;

/// <summary>
/// Normalização do termo de busca usado contra colunas <c>*_normalizado</c>:
/// remove diacríticos no app e escapa curingas de LIKE antes de montar o padrão
/// <c>%termo%</c>.
/// </summary>
internal static class BuscaTextualNormalizada
{
    public const string Escape = @"\";

    public static string CriarPadraoContem(string termo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(termo);
        return "%" + EscaparCuringasLike(RemoverDiacriticos(termo.Trim())) + "%";
    }

    private static string RemoverDiacriticos(string texto)
    {
        string decomposto = texto.Normalize(NormalizationForm.FormD);
        return new string([.. decomposto.Where(c =>
            CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)]);
    }

    private static string EscaparCuringasLike(string termo) =>
        termo
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}
