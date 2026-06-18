namespace Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Readers;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

/// <summary>
/// Normalização do termo de busca usado contra colunas <c>*_normalizado</c>: remove
/// diacríticos e baixa a caixa no app (espelhando o que <c>GeoTexto</c> gravou na coluna),
/// escapando curingas de LIKE para montar o padrão <c>%termo%</c>. O termo canonicalizado
/// também serve o ranking por similaridade trigram (<c>similarity</c>), que compara texto
/// literal e por isso precisa casar a caixa/acento da coluna.
/// </summary>
internal static class BuscaTextualNormalizada
{
    public const string Escape = @"\";

    /// <summary>
    /// Termo canonicalizado (sem acento, caixa-baixa, sem curingas escapados) — entrada do
    /// <c>similarity</c> do ranking, comparado contra a coluna <c>nome_normalizado</c>.
    /// </summary>
    public static string Normalizar(string termo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(termo);
        return MinusculaSemAcento(termo.Trim());
    }

    public static string CriarPadraoContem(string termo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(termo);
        return "%" + EscaparCuringasLike(MinusculaSemAcento(termo.Trim())) + "%";
    }

    // Remove diacríticos e baixa a caixa: o ILIKE é caixa-insensível (a caixa não altera o
    // filtro), mas o similarity é caixa-sensível e precisa casar a coluna já em caixa-baixa.
    [SuppressMessage(
        "Globalization",
        "CA1308:Normalize strings to uppercase",
        Justification = "Casa a coluna *_normalizado (caixa-baixa, GeoTexto) para o similarity trigram; não é round-trip de identidade.")]
    private static string MinusculaSemAcento(string texto) =>
        RemoverDiacriticos(texto).ToLowerInvariant();

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
