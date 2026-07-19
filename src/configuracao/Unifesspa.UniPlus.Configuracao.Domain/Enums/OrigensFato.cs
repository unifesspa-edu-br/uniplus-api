namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Mapeamento entre <see cref="OrigemFato"/> (PascalCase) e o token textual de
/// contrato/banco (UPPER_SNAKE), com parsing de domínio fechado por allowlist
/// explícita (molde de <see cref="NaturezasLegais"/>). Fonte do CHECK de domínio
/// em <c>rol_de_fatos_candidato.origem</c> e do value converter de persistência.
/// </summary>
public static class OrigensFato
{
    private static readonly Dictionary<OrigemFato, string> ParaToken = new()
    {
        [OrigemFato.Derivado] = "DERIVADO",
        [OrigemFato.Declarado] = "DECLARADO",
        [OrigemFato.Integracao] = "INTEGRACAO",
    };

    private static readonly Dictionary<string, OrigemFato> DeToken =
        ParaToken.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.Ordinal);

    /// <summary>Os três tokens canônicos (UPPER_SNAKE), para o CHECK de domínio e mensagens.</summary>
    public static readonly IReadOnlyList<string> TokensCanonicos = [.. ParaToken.Values];

    /// <summary>Token textual de contrato/banco (UPPER_SNAKE) de uma origem válida.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Se <paramref name="origem"/> é <see cref="OrigemFato.Nenhuma"/> ou fora do roster.</exception>
    public static string ParaTokenCanonico(OrigemFato origem) =>
        ParaToken.TryGetValue(origem, out string? token)
            ? token
            : throw new ArgumentOutOfRangeException(
                nameof(origem), origem, "Origem de fato fora do domínio fechado.");

    /// <summary>Resolve um token textual (UPPER_SNAKE) à origem; <see langword="false"/> quando inválido.</summary>
    public static bool TryAnalisar(string? token, out OrigemFato origem)
    {
        if (!string.IsNullOrWhiteSpace(token)
            && DeToken.TryGetValue(token.Trim(), out OrigemFato resolvida))
        {
            origem = resolvida;
            return true;
        }

        origem = OrigemFato.Nenhuma;
        return false;
    }

    /// <summary>Resolve um token à sua enum (reidratação fail-fast do value converter).</summary>
    /// <exception cref="ArgumentOutOfRangeException">Se <paramref name="token"/> não é canônico.</exception>
    public static OrigemFato Analisar(string? token) =>
        TryAnalisar(token, out OrigemFato origem)
            ? origem
            : throw new ArgumentOutOfRangeException(
                nameof(token), token, "Token de origem de fato fora do domínio fechado.");

    /// <summary>Indica se <paramref name="token"/> é um dos três tokens canônicos.</summary>
    public static bool EhValido(string? token) => TryAnalisar(token, out _);
}
