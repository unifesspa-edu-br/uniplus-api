namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Mapeamento entre <see cref="Abrangencia"/> (domínio, PascalCase) e o token
/// textual de contrato/banco (UPPER_SNAKE), com parsing de domínio fechado.
/// </summary>
/// <remarks>
/// Parsing por <b>allowlist textual explícita</b> (<see cref="TryAnalisar"/>) — não usa
/// <c>Enum.TryParse</c>, que aceitaria tokens numéricos e nomes PascalCase fora do
/// contrato textual.
/// </remarks>
public static class Abrangencias
{
    private static readonly Dictionary<Abrangencia, string> ParaToken = new()
    {
        [Abrangencia.Nacional] = "NACIONAL",
        [Abrangencia.Estadual] = "ESTADUAL",
        [Abrangencia.Municipal] = "MUNICIPAL",
        [Abrangencia.Institucional] = "INSTITUCIONAL",
    };

    private static readonly Dictionary<string, Abrangencia> DeToken =
        ParaToken.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.Ordinal);

    /// <summary>Os quatro tokens canônicos (UPPER_SNAKE), para o CHECK de domínio e mensagens.</summary>
    public static readonly IReadOnlyList<string> TokensCanonicos = [.. ParaToken.Values];

    /// <summary>Token textual de contrato/banco (UPPER_SNAKE) de uma abrangência válida.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Se <paramref name="abrangencia"/> é <see cref="Abrangencia.Nenhuma"/> ou fora do roster.</exception>
    public static string ParaTokenCanonico(Abrangencia abrangencia) =>
        ParaToken.TryGetValue(abrangencia, out string? token)
            ? token
            : throw new ArgumentOutOfRangeException(
                nameof(abrangencia), abrangencia, "Abrangência fora do domínio fechado.");

    /// <summary>
    /// Resolve um token textual (UPPER_SNAKE) à abrangência correspondente. Aceita
    /// <c>Trim</c>, mas é case-sensitive e rejeita tokens fora do domínio (allowlist).
    /// Retorna <see langword="false"/> quando inválido.
    /// </summary>
    public static bool TryAnalisar(string? token, out Abrangencia abrangencia)
    {
        if (!string.IsNullOrWhiteSpace(token)
            && DeToken.TryGetValue(token.Trim(), out Abrangencia resolvida))
        {
            abrangencia = resolvida;
            return true;
        }

        abrangencia = Abrangencia.Nenhuma;
        return false;
    }

    /// <summary>Indica se <paramref name="token"/> é um dos tokens canônicos, sem alocar resultado.</summary>
    public static bool EhValido(string? token) => TryAnalisar(token, out _);
}
