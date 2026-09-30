namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Mapeamento entre <see cref="FonteValoresFato"/> (PascalCase) e o token textual de
/// contrato/banco (UPPER_SNAKE), com parsing de domínio fechado por allowlist explícita (molde
/// de <see cref="CardinalidadesFato"/>). Fonte do CHECK de domínio em
/// <c>rol_de_fatos_candidato.fonte_valores</c> e do value converter de persistência.
/// </summary>
public static class FontesValoresFato
{
    private static readonly Dictionary<FonteValoresFato, string> ParaToken = new()
    {
        [FonteValoresFato.Global] = "GLOBAL",
        [FonteValoresFato.Processo] = "PROCESSO",
        [FonteValoresFato.Modalidade] = "MODALIDADE",
    };

    private static readonly Dictionary<string, FonteValoresFato> DeToken =
        ParaToken.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.Ordinal);

    /// <summary>Os tokens canônicos (UPPER_SNAKE), para o CHECK de domínio e mensagens.</summary>
    public static readonly IReadOnlyList<string> TokensCanonicos = [.. ParaToken.Values];

    /// <summary>Token textual de contrato/banco (UPPER_SNAKE) de uma fonte válida.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Se <paramref name="fonte"/> é <see cref="FonteValoresFato.Nenhuma"/> ou fora do roster.</exception>
    public static string ParaTokenCanonico(FonteValoresFato fonte) =>
        ParaToken.TryGetValue(fonte, out string? token)
            ? token
            : throw new ArgumentOutOfRangeException(nameof(fonte), fonte, "Fonte dos valores fora do domínio fechado.");

    /// <summary>Resolve um token textual (UPPER_SNAKE) à fonte; <see langword="false"/> quando inválido.</summary>
    public static bool TryAnalisar(string? token, out FonteValoresFato fonte)
    {
        if (!string.IsNullOrWhiteSpace(token) && DeToken.TryGetValue(token.Trim(), out FonteValoresFato resolvida))
        {
            fonte = resolvida;
            return true;
        }

        fonte = FonteValoresFato.Nenhuma;
        return false;
    }

    /// <summary>Resolve um token à sua enum (reidratação fail-fast do value converter).</summary>
    /// <exception cref="ArgumentOutOfRangeException">Se <paramref name="token"/> não é canônico.</exception>
    public static FonteValoresFato Analisar(string? token) =>
        TryAnalisar(token, out FonteValoresFato fonte)
            ? fonte
            : throw new ArgumentOutOfRangeException(nameof(token), token, "Token de fonte dos valores fora do domínio fechado.");
}
