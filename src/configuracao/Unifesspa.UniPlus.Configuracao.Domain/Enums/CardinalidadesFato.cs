namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Mapeamento entre <see cref="CardinalidadeFato"/> (PascalCase) e o token textual
/// de contrato/banco (UPPER_SNAKE), com parsing de domínio fechado por allowlist
/// explícita (molde de <see cref="NaturezasLegais"/>). Fonte do CHECK de domínio
/// em <c>rol_de_fatos_candidato.cardinalidade</c> e do value converter de persistência.
/// </summary>
public static class CardinalidadesFato
{
    private static readonly Dictionary<CardinalidadeFato, string> ParaToken = new()
    {
        [CardinalidadeFato.Escalar] = "ESCALAR",
        [CardinalidadeFato.Multivalorado] = "MULTIVALORADO",
    };

    private static readonly Dictionary<string, CardinalidadeFato> DeToken =
        ParaToken.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.Ordinal);

    /// <summary>Os dois tokens canônicos (UPPER_SNAKE), para o CHECK de domínio e mensagens.</summary>
    public static readonly IReadOnlyList<string> TokensCanonicos = [.. ParaToken.Values];

    /// <summary>Token textual de contrato/banco (UPPER_SNAKE) de uma cardinalidade válida.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Se <paramref name="cardinalidade"/> é <see cref="CardinalidadeFato.Nenhuma"/> ou fora do roster.</exception>
    public static string ParaTokenCanonico(CardinalidadeFato cardinalidade) =>
        ParaToken.TryGetValue(cardinalidade, out string? token)
            ? token
            : throw new ArgumentOutOfRangeException(
                nameof(cardinalidade), cardinalidade, "Cardinalidade de fato fora do domínio fechado.");

    /// <summary>Resolve um token textual (UPPER_SNAKE) à cardinalidade; <see langword="false"/> quando inválido.</summary>
    public static bool TryAnalisar(string? token, out CardinalidadeFato cardinalidade)
    {
        if (!string.IsNullOrWhiteSpace(token)
            && DeToken.TryGetValue(token.Trim(), out CardinalidadeFato resolvida))
        {
            cardinalidade = resolvida;
            return true;
        }

        cardinalidade = CardinalidadeFato.Nenhuma;
        return false;
    }

    /// <summary>Resolve um token à sua enum (reidratação fail-fast do value converter).</summary>
    /// <exception cref="ArgumentOutOfRangeException">Se <paramref name="token"/> não é canônico.</exception>
    public static CardinalidadeFato Analisar(string? token) =>
        TryAnalisar(token, out CardinalidadeFato cardinalidade)
            ? cardinalidade
            : throw new ArgumentOutOfRangeException(
                nameof(token), token, "Token de cardinalidade de fato fora do domínio fechado.");

    /// <summary>Indica se <paramref name="token"/> é um dos dois tokens canônicos.</summary>
    public static bool EhValido(string? token) => TryAnalisar(token, out _);
}
