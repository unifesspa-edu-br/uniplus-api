namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Mapeamento entre <see cref="CategoriaDocumento"/> (domínio, PascalCase) e o
/// token textual de contrato/banco (UPPER_SNAKE), com parsing de domínio fechado.
/// </summary>
/// <remarks>
/// <para>O parsing é por <b>allowlist textual explícita</b> (<see cref="TryAnalisar"/>):
/// só os sete tokens canônicos são aceitos. Deliberadamente <b>não</b> usa
/// <c>Enum.TryParse</c>, que aceitaria tokens numéricos (<c>"1"</c> → primeiro
/// valor) e nomes PascalCase do enum — ambos fora do contrato textual da #591.</para>
/// <para>É o vocabulário fonte do CHECK de domínio em <c>tipo_documento.categoria</c>
/// (<see cref="TokensCanonicos"/>) e do value converter de persistência.</para>
/// </remarks>
public static class CategoriaDocumentos
{
    private static readonly Dictionary<CategoriaDocumento, string> ParaToken = new()
    {
        [CategoriaDocumento.Identificacao] = "IDENTIFICACAO",
        [CategoriaDocumento.Escolaridade] = "ESCOLARIDADE",
        [CategoriaDocumento.Renda] = "RENDA",
        [CategoriaDocumento.RacaEtnia] = "RACA_ETNIA",
        [CategoriaDocumento.Saude] = "SAUDE",
        [CategoriaDocumento.Residencia] = "RESIDENCIA",
        [CategoriaDocumento.Outros] = "OUTROS",
    };

    private static readonly Dictionary<string, CategoriaDocumento> DeToken =
        ParaToken.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.Ordinal);

    /// <summary>Os sete tokens canônicos (UPPER_SNAKE), para o CHECK de domínio e mensagens.</summary>
    public static readonly IReadOnlyList<string> TokensCanonicos = [.. ParaToken.Values];

    /// <summary>Token textual de contrato/banco (UPPER_SNAKE) de uma categoria válida.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Se <paramref name="categoria"/> é <see cref="CategoriaDocumento.Nenhum"/> ou fora do roster.</exception>
    public static string ParaTokenCanonico(CategoriaDocumento categoria) =>
        ParaToken.TryGetValue(categoria, out string? token)
            ? token
            : throw new ArgumentOutOfRangeException(
                nameof(categoria), categoria, "Categoria de documento fora do domínio fechado.");

    /// <summary>
    /// Resolve um token textual (UPPER_SNAKE) à categoria correspondente. Aceita
    /// <c>Trim</c>, mas é case-sensitive e rejeita tokens numéricos ou fora do
    /// domínio (allowlist). Retorna <see langword="false"/> quando inválido.
    /// </summary>
    public static bool TryAnalisar(string? token, out CategoriaDocumento categoria)
    {
        if (!string.IsNullOrWhiteSpace(token)
            && DeToken.TryGetValue(token.Trim(), out CategoriaDocumento resolvida))
        {
            categoria = resolvida;
            return true;
        }

        categoria = CategoriaDocumento.Nenhum;
        return false;
    }

    /// <summary>Indica se <paramref name="token"/> é um dos sete tokens canônicos, sem alocar resultado.</summary>
    public static bool EhValido(string? token) => TryAnalisar(token, out _);
}
