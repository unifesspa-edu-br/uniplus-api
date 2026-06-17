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
    /// Trim de string obrigatória (não nula). Usado por <c>*_normalizado</c> que
    /// compõe chave natural (Distrito/Bairro/Logradouro): o valor já vem
    /// sem acento e em caixa consistente da fonte (<c>*_sem_acento</c> da DNE),
    /// então só o trim é aplicado — preservando a forma da fonte para idempotência.
    /// </summary>
    public static string NormalizarTexto(string valor) => valor.Trim();
}
