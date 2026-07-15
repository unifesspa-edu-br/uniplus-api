namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Mapeamento entre <see cref="OrigemDataFase"/> (domínio, PascalCase) e o token
/// textual de contrato/banco (UPPER_SNAKE), com parsing de domínio fechado.
/// </summary>
/// <remarks>
/// <para>O parsing é por <b>allowlist textual explícita</b> (<see cref="TryAnalisar"/>):
/// só os dois tokens canônicos são aceitos. Deliberadamente <b>não</b> usa
/// <c>Enum.TryParse</c>, que aceitaria tokens numéricos e nomes PascalCase do enum —
/// mesmo molde de <see cref="DonosTipicos"/>.</para>
/// <para>É o vocabulário fonte do CHECK de domínio em <c>fase_canonica.origem_data</c>
/// (<see cref="TokensCanonicos"/>) e do value converter de persistência.</para>
/// </remarks>
public static class OrigensDataFase
{
    private static readonly Dictionary<OrigemDataFase, string> ParaToken = new()
    {
        [OrigemDataFase.Propria] = "PROPRIA",
        [OrigemDataFase.Delegada] = "DELEGADA",
    };

    private static readonly Dictionary<string, OrigemDataFase> DeToken =
        ParaToken.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.Ordinal);

    /// <summary>Os dois tokens canônicos (UPPER_SNAKE), para o CHECK de domínio e mensagens.</summary>
    public static readonly IReadOnlyList<string> TokensCanonicos = [.. ParaToken.Values];

    /// <summary>Token textual de contrato/banco (UPPER_SNAKE) de uma origem de data válida.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Se <paramref name="origem"/> é <see cref="OrigemDataFase.Nenhuma"/> ou fora do roster.</exception>
    public static string ParaTokenCanonico(OrigemDataFase origem) =>
        ParaToken.TryGetValue(origem, out string? token)
            ? token
            : throw new ArgumentOutOfRangeException(
                nameof(origem), origem, "Origem da data fora do domínio fechado.");

    /// <summary>
    /// Resolve um token textual (UPPER_SNAKE) à origem correspondente. Aceita
    /// <c>Trim</c>, mas é case-sensitive e rejeita tokens numéricos ou fora do
    /// domínio (allowlist). Retorna <see langword="false"/> quando inválido.
    /// </summary>
    public static bool TryAnalisar(string? token, out OrigemDataFase origem)
    {
        if (!string.IsNullOrWhiteSpace(token)
            && DeToken.TryGetValue(token.Trim(), out OrigemDataFase resolvida))
        {
            origem = resolvida;
            return true;
        }

        origem = OrigemDataFase.Nenhuma;
        return false;
    }

    /// <summary>Indica se <paramref name="token"/> é um dos dois tokens canônicos, sem alocar resultado.</summary>
    public static bool EhValido(string? token) => TryAnalisar(token, out _);
}
