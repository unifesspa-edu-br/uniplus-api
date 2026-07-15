namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Mapeamento entre <see cref="DominioFato"/> (domínio, PascalCase) e o token
/// textual de contrato/banco (UPPER_SNAKE), com parsing de domínio fechado por
/// allowlist explícita (molde de <see cref="NaturezasLegais"/>). Fonte do CHECK de
/// domínio em <c>rol_de_fatos_candidato.dominio</c> e do value converter de persistência.
/// </summary>
public static class DominiosFato
{
    private static readonly Dictionary<DominioFato, string> ParaToken = new()
    {
        [DominioFato.Categorico] = "CATEGORICO",
        [DominioFato.Booleano] = "BOOLEANO",
        [DominioFato.Numerico] = "NUMERICO",
    };

    private static readonly Dictionary<string, DominioFato> DeToken =
        ParaToken.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.Ordinal);

    /// <summary>Os três tokens canônicos (UPPER_SNAKE), para o CHECK de domínio e mensagens.</summary>
    public static readonly IReadOnlyList<string> TokensCanonicos = [.. ParaToken.Values];

    /// <summary>Token textual de contrato/banco (UPPER_SNAKE) de um domínio válido.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Se <paramref name="dominio"/> é <see cref="DominioFato.Nenhum"/> ou fora do roster.</exception>
    public static string ParaTokenCanonico(DominioFato dominio) =>
        ParaToken.TryGetValue(dominio, out string? token)
            ? token
            : throw new ArgumentOutOfRangeException(
                nameof(dominio), dominio, "Domínio de fato fora do domínio fechado.");

    /// <summary>Resolve um token textual (UPPER_SNAKE) ao domínio; <see langword="false"/> quando inválido.</summary>
    public static bool TryAnalisar(string? token, out DominioFato dominio)
    {
        if (!string.IsNullOrWhiteSpace(token)
            && DeToken.TryGetValue(token.Trim(), out DominioFato resolvido))
        {
            dominio = resolvido;
            return true;
        }

        dominio = DominioFato.Nenhum;
        return false;
    }

    /// <summary>Resolve um token à sua enum (reidratação fail-fast do value converter).</summary>
    /// <exception cref="ArgumentOutOfRangeException">Se <paramref name="token"/> não é canônico.</exception>
    public static DominioFato Analisar(string? token) =>
        TryAnalisar(token, out DominioFato dominio)
            ? dominio
            : throw new ArgumentOutOfRangeException(
                nameof(token), token, "Token de domínio de fato fora do domínio fechado.");

    /// <summary>Indica se <paramref name="token"/> é um dos três tokens canônicos.</summary>
    public static bool EhValido(string? token) => TryAnalisar(token, out _);
}
