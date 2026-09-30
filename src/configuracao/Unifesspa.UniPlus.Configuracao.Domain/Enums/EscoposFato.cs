namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Mapeamento entre <see cref="EscopoFato"/> (PascalCase) e o token textual de contrato/banco
/// (UPPER_SNAKE), com parsing de domínio fechado por allowlist explícita (molde de
/// <see cref="CardinalidadesFato"/>). Fonte do CHECK de domínio em
/// <c>rol_de_fatos_candidato.escopo</c> e do value converter de persistência.
/// </summary>
public static class EscoposFato
{
    private static readonly Dictionary<EscopoFato, string> ParaToken = new()
    {
        [EscopoFato.Candidato] = "CANDIDATO",
        [EscopoFato.MembroGrupo] = "MEMBRO_GRUPO",
    };

    private static readonly Dictionary<string, EscopoFato> DeToken =
        ParaToken.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.Ordinal);

    /// <summary>Os tokens canônicos (UPPER_SNAKE), para o CHECK de domínio e mensagens.</summary>
    public static readonly IReadOnlyList<string> TokensCanonicos = [.. ParaToken.Values];

    /// <summary>Token textual de contrato/banco (UPPER_SNAKE) de um valor válido.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Se o valor é o sentinela ou está fora do roster.</exception>
    public static string ParaTokenCanonico(EscopoFato valor) =>
        ParaToken.TryGetValue(valor, out string? token)
            ? token
            : throw new ArgumentOutOfRangeException(nameof(valor), valor, "Escopo do fato fora do domínio fechado.");

    /// <summary>Resolve um token textual (UPPER_SNAKE); <see langword="false"/> quando inválido.</summary>
    public static bool TryAnalisar(string? token, out EscopoFato valor)
    {
        if (!string.IsNullOrWhiteSpace(token) && DeToken.TryGetValue(token.Trim(), out EscopoFato resolvido))
        {
            valor = resolvido;
            return true;
        }

        valor = EscopoFato.Nenhum;
        return false;
    }

    /// <summary>Resolve um token à sua enum (reidratação fail-fast do value converter).</summary>
    /// <exception cref="ArgumentOutOfRangeException">Se <paramref name="token"/> não é canônico.</exception>
    public static EscopoFato Analisar(string? token) =>
        TryAnalisar(token, out EscopoFato valor)
            ? valor
            : throw new ArgumentOutOfRangeException(nameof(token), token, "Token de escopo do fato fora do domínio fechado.");
}
