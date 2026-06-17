namespace Unifesspa.UniPlus.Geo.Domain.Entities;

/// <summary>
/// Helpers de normalização de texto compartilhados pelas factories <c>Importar</c>
/// das entidades de localidade do Geo. Mantém o tratamento de strings opcionais
/// consistente (trim + vazio → <see langword="null"/>) sem acoplar uma entidade à
/// outra.
/// </summary>
internal static class GeoTexto
{
    /// <summary>Trim de string opcional; vazio/branco vira <see langword="null"/>.</summary>
    public static string? NormalizarOpcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    /// <summary>
    /// Normaliza uma chave natural alfabética (sigla/UF): trim + maiúsculas
    /// invariantes. Os índices únicos são case-sensitive (<c>text</c>), então sem
    /// isso <c>BRA</c>/<c>bra</c> entrariam como linhas distintas e a idempotência
    /// do upsert do ETL quebraria. (Maiúsculas, não minúsculas, por CA1308.)
    /// </summary>
    public static string NormalizarChaveMaiuscula(string valor) =>
        valor.Trim().ToUpperInvariant();

    /// <summary>
    /// Canonicaliza um campo <c>*_normalizado</c> obrigatório (compõe chave natural
    /// em Distrito/Bairro/Logradouro/Complemento): trim + minúsculas. As UNIQUEs em
    /// <c>text</c> são case-sensitive, então sem canonicalizar a caixa "Centro" e
    /// "centro" entrariam como chaves distintas e furariam a idempotência do upsert.
    /// </summary>
    public static string NormalizarTexto(string valor) => Minuscula(valor.Trim());

    /// <summary>
    /// Variante opcional de <see cref="NormalizarTexto"/> para <c>*_normalizado</c>
    /// que NÃO compõe chave (Cidade/Estado/CepGrandeUsuario): mantém a coluna de busca
    /// canônica (minúsculas), coerente com os campos-chave; vazio vira <see langword="null"/>.
    /// </summary>
    public static string? NormalizarBuscaOpcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : Minuscula(valor.Trim());

    // Caixa-baixa para campos *_normalizado (busca/chave acento- e caixa-insensível).
    // CA1308: minúsculas — e não maiúsculas — porque a fonte DNE (*_sem_acento) é
    // minúscula e a comparação trigram (ILIKE) é caixa-insensível; não há round-trip
    // de identidade nem dado sensível aqui (são chaves de busca derivadas).
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Globalization",
        "CA1308:Normalize strings to uppercase",
        Justification = "Campos *_normalizado canonicalizam em minúsculas para casar com a fonte DNE e a busca trigram ILIKE; não é round-trip de identidade.")]
    private static string Minuscula(string valor) => valor.ToLowerInvariant();
}
