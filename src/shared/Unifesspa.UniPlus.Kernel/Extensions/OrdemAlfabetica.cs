namespace Unifesspa.UniPlus.Kernel.Extensions;

using System.Globalization;
using System.Text;

/// <summary>
/// Ordem alfabética determinística de nomes em português, independente da cultura do host: compara
/// sem acento nem caixa, para "Pará" vir antes de "Paraíba" e "Água Azul do Norte", antes de
/// "Marabá".
/// </summary>
public static class OrdemAlfabetica
{
    /// <summary>O nome sem acento e em caixa baixa invariante, para ordenar com comparação ordinal.</summary>
    public static string Chave(string nome)
    {
        ArgumentNullException.ThrowIfNull(nome);

        return string.Concat(nome.Normalize(NormalizationForm.FormD)
            .Where(static c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .Select(char.ToLowerInvariant));
    }
}
