namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Mapeamento entre <see cref="NaturezaFato"/> (PascalCase) e o token textual de
/// contrato/banco (UPPER_SNAKE), com parsing de domínio fechado por allowlist
/// explícita (molde de <see cref="NaturezasLegais"/>). Fonte do CHECK de domínio
/// em <c>rol_de_fatos_candidato.natureza</c> e do value converter de persistência.
/// </summary>
public static class NaturezasFato
{
    private static readonly Dictionary<NaturezaFato, string> ParaToken = new()
    {
        [NaturezaFato.BrutoInformado] = "BRUTO_INFORMADO",
        [NaturezaFato.DeVontade] = "DE_VONTADE",
        [NaturezaFato.Derivado] = "DERIVADO",
    };

    private static readonly Dictionary<string, NaturezaFato> DeToken =
        ParaToken.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.Ordinal);

    /// <summary>Os três tokens canônicos (UPPER_SNAKE), para o CHECK de domínio e mensagens.</summary>
    public static readonly IReadOnlyList<string> TokensCanonicos = [.. ParaToken.Values];

    /// <summary>Token textual de contrato/banco (UPPER_SNAKE) de uma natureza válida.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Se <paramref name="natureza"/> é <see cref="NaturezaFato.Nenhuma"/> ou fora do roster.</exception>
    public static string ParaTokenCanonico(NaturezaFato natureza) =>
        ParaToken.TryGetValue(natureza, out string? token)
            ? token
            : throw new ArgumentOutOfRangeException(
                nameof(natureza), natureza, "Natureza de fato fora do domínio fechado.");

    /// <summary>Resolve um token textual (UPPER_SNAKE) à natureza; <see langword="false"/> quando inválido.</summary>
    public static bool TryAnalisar(string? token, out NaturezaFato natureza)
    {
        if (!string.IsNullOrWhiteSpace(token)
            && DeToken.TryGetValue(token.Trim(), out NaturezaFato resolvida))
        {
            natureza = resolvida;
            return true;
        }

        natureza = NaturezaFato.Nenhuma;
        return false;
    }

    /// <summary>Resolve um token à sua enum (reidratação fail-fast do value converter).</summary>
    /// <exception cref="ArgumentOutOfRangeException">Se <paramref name="token"/> não é canônico.</exception>
    public static NaturezaFato Analisar(string? token) =>
        TryAnalisar(token, out NaturezaFato natureza)
            ? natureza
            : throw new ArgumentOutOfRangeException(
                nameof(token), token, "Token de natureza de fato fora do domínio fechado.");

    /// <summary>Indica se <paramref name="token"/> é um dos três tokens canônicos.</summary>
    public static bool EhValido(string? token) => TryAnalisar(token, out _);
}
