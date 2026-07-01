namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Mapeamento entre <see cref="DonoTipico"/> (domínio, PascalCase) e o token
/// textual de contrato/banco (UPPER_SNAKE), com parsing de domínio fechado.
/// </summary>
/// <remarks>
/// <para>O parsing é por <b>allowlist textual explícita</b> (<see cref="TryAnalisar"/>):
/// só os quatro tokens canônicos são aceitos. Deliberadamente <b>não</b> usa
/// <c>Enum.TryParse</c>, que aceitaria tokens numéricos e nomes PascalCase do enum
/// — ambos fora do contrato textual da #592.</para>
/// <para>É o vocabulário fonte do CHECK de domínio em <c>fase_canonica.dono_tipico</c>
/// (<see cref="TokensCanonicos"/>) e do value converter de persistência.</para>
/// </remarks>
public static class DonosTipicos
{
    private static readonly Dictionary<DonoTipico, string> ParaToken = new()
    {
        [DonoTipico.Ceps] = "CEPS",
        [DonoTipico.Crca] = "CRCA",
        [DonoTipico.Mec] = "MEC",
        [DonoTipico.Consepe] = "CONSEPE",
    };

    private static readonly Dictionary<string, DonoTipico> DeToken =
        ParaToken.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.Ordinal);

    /// <summary>Os quatro tokens canônicos (UPPER_SNAKE), para o CHECK de domínio e mensagens.</summary>
    public static readonly IReadOnlyList<string> TokensCanonicos = [.. ParaToken.Values];

    /// <summary>Token textual de contrato/banco (UPPER_SNAKE) de um dono válido.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Se <paramref name="dono"/> é <see cref="DonoTipico.Nenhum"/> ou fora do roster.</exception>
    public static string ParaTokenCanonico(DonoTipico dono) =>
        ParaToken.TryGetValue(dono, out string? token)
            ? token
            : throw new ArgumentOutOfRangeException(
                nameof(dono), dono, "Dono típico fora do domínio fechado.");

    /// <summary>
    /// Resolve um token textual (UPPER_SNAKE) ao dono correspondente. Aceita
    /// <c>Trim</c>, mas é case-sensitive e rejeita tokens numéricos ou fora do
    /// domínio (allowlist). Retorna <see langword="false"/> quando inválido.
    /// </summary>
    public static bool TryAnalisar(string? token, out DonoTipico dono)
    {
        if (!string.IsNullOrWhiteSpace(token)
            && DeToken.TryGetValue(token.Trim(), out DonoTipico resolvido))
        {
            dono = resolvido;
            return true;
        }

        dono = DonoTipico.Nenhum;
        return false;
    }

    /// <summary>Indica se <paramref name="token"/> é um dos quatro tokens canônicos, sem alocar resultado.</summary>
    public static bool EhValido(string? token) => TryAnalisar(token, out _);
}
