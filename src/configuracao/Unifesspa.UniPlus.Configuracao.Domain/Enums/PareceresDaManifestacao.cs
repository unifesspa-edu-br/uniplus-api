namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Mapeamento entre <see cref="ParecerDaManifestacao"/> e o token textual de contrato/banco (UPPER_SNAKE), com
/// parsing de domínio fechado por allowlist explícita (molde de <see cref="AtosDaProtecao"/>).
/// </summary>
public static class PareceresDaManifestacao
{
    /// <summary>Token de <see cref="ParecerDaManifestacao.Adequada"/>.</summary>
    public const string Adequada = "ADEQUADA";

    /// <summary>Token de <see cref="ParecerDaManifestacao.RecomendaAjuste"/>.</summary>
    public const string RecomendaAjuste = "RECOMENDA_AJUSTE";

    /// <summary>Token de <see cref="ParecerDaManifestacao.Inadequada"/>.</summary>
    public const string Inadequada = "INADEQUADA";

    private static readonly Dictionary<ParecerDaManifestacao, string> ParaToken = new()
    {
        [ParecerDaManifestacao.Adequada] = Adequada,
        [ParecerDaManifestacao.RecomendaAjuste] = RecomendaAjuste,
        [ParecerDaManifestacao.Inadequada] = Inadequada,
    };

    private static readonly Dictionary<string, ParecerDaManifestacao> DeToken =
        ParaToken.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.Ordinal);

    /// <summary>Os tokens canônicos (UPPER_SNAKE), para o CHECK de domínio e mensagens.</summary>
    public static readonly IReadOnlyList<string> TokensCanonicos = [.. ParaToken.Values];

    /// <summary>
    /// Recomenda ajuste e inadequada: os pareceres que exigem o texto e que, vigentes, exigem a justificativa de quem
    /// aprova ou recomenda ajuste (UNI-REQ-0374).
    /// </summary>
    public static bool EhDesfavoravel(ParecerDaManifestacao parecer) =>
        parecer is ParecerDaManifestacao.RecomendaAjuste or ParecerDaManifestacao.Inadequada;

    /// <summary>Token textual de contrato/banco de um parecer válido.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Se <paramref name="parecer"/> é o sentinela ou está fora do roster.</exception>
    public static string ParaTokenCanonico(ParecerDaManifestacao parecer) =>
        ParaToken.TryGetValue(parecer, out string? token)
            ? token
            : throw new ArgumentOutOfRangeException(nameof(parecer), parecer, "Parecer da manifestação fora do domínio fechado.");

    /// <summary>Resolve um token textual ao parecer; <see langword="false"/> quando inválido.</summary>
    public static bool TryAnalisar(string? token, out ParecerDaManifestacao parecer)
    {
        if (!string.IsNullOrWhiteSpace(token) && DeToken.TryGetValue(token.Trim(), out ParecerDaManifestacao resolvido))
        {
            parecer = resolvido;
            return true;
        }

        parecer = ParecerDaManifestacao.Nenhum;
        return false;
    }

    /// <summary>Resolve um token ao seu enum (reidratação fail-fast do value converter).</summary>
    /// <exception cref="ArgumentOutOfRangeException">Se <paramref name="token"/> não é canônico.</exception>
    public static ParecerDaManifestacao Analisar(string? token) =>
        TryAnalisar(token, out ParecerDaManifestacao parecer)
            ? parecer
            : throw new ArgumentOutOfRangeException(nameof(token), token, "Token de parecer da manifestação fora do domínio fechado.");
}
