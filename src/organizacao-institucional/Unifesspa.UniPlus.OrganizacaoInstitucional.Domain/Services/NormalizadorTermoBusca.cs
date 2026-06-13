namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Services;

using System.Globalization;
using System.Text;

/// <summary>
/// Normaliza texto para busca insensível a acento e caixa, com a mesma
/// semântica do <c>normalizarBusca</c> do frontend (uniplus-web): decompõe em
/// forma canônica (NFD), remove marcas diacríticas combinantes, dobra a caixa
/// e apara as pontas. Mantém um único algoritmo de verdade entre o termo de
/// consulta e o índice de busca persistido na <c>Unidade</c>.
/// </summary>
/// <remarks>
/// Para os caracteres do domínio (nomes institucionais em pt-BR) a remoção de
/// diacríticos coincide com a do frontend: <c>'á'→'a'</c>, <c>'ç'→'c'</c>,
/// <c>'ã'→'a'</c> etc. — a decomposição NFD separa o caractere base da marca
/// combinante (categoria Unicode <see cref="UnicodeCategory.NonSpacingMark"/>),
/// que é então descartada. A dobra de caixa usa <c>ToUpperInvariant</c>
/// (direção segura recomendada — regra CA1308 — e convenção do agregado, que
/// já normaliza a Sigla em maiúsculas): como tanto o índice persistido quanto
/// o termo de consulta passam por este mesmo método, a busca é caixa-insensível
/// e auto-consistente. O frontend filtra no cliente e nunca envia o valor
/// normalizado, então o alinhamento é de semântica, não de literal.
/// </remarks>
public static class NormalizadorTermoBusca
{
    /// <summary>
    /// Normaliza um termo livre. Retorna <see cref="string.Empty"/> para
    /// entrada nula ou em branco — sinaliza ao chamador "sem filtro".
    /// </summary>
    public static string Normalizar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return string.Empty;
        }

        string decomposto = valor.Normalize(NormalizationForm.FormD);

        char[] semDiacriticos = [.. decomposto.Where(caractere =>
            CharUnicodeInfo.GetUnicodeCategory(caractere) != UnicodeCategory.NonSpacingMark)];

        return new string(semDiacriticos).ToUpperInvariant().Trim();
    }

    /// <summary>
    /// Monta o índice de busca desnormalizado de uma Unidade concatenando os
    /// campos pesquisáveis (nome, sigla, código, slug, alias) com espaço e
    /// aplicando <see cref="Normalizar"/> — idêntico à concatenação que o
    /// frontend normaliza para o filtro client-side.
    /// </summary>
    public static string ParaIndice(string nome, string sigla, string codigo, string slug, string? alias)
    {
        return Normalizar($"{nome} {sigla} {codigo} {slug} {alias}");
    }
}
