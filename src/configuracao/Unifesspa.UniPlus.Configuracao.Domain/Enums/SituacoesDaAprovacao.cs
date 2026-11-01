namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Mapeamento entre <see cref="SituacaoDaAprovacao"/> (PascalCase) e o token textual de
/// contrato/banco (UPPER_SNAKE), com parsing de domínio fechado por allowlist explícita (molde de
/// <see cref="CardinalidadesFato"/>). Fonte do CHECK de domínio da situação e do value converter
/// de persistência.
/// </summary>
public static class SituacoesDaAprovacao
{
    private static readonly Dictionary<SituacaoDaAprovacao, string> ParaToken = new()
    {
        [SituacaoDaAprovacao.Pendente] = "PENDENTE",
        [SituacaoDaAprovacao.Solicitada] = "SOLICITADA",
        [SituacaoDaAprovacao.Aprovada] = "APROVADA",
        [SituacaoDaAprovacao.AjusteRecomendado] = "AJUSTE_RECOMENDADO",
        [SituacaoDaAprovacao.Reprovada] = "REPROVADA",
    };

    private static readonly Dictionary<string, SituacaoDaAprovacao> DeToken =
        ParaToken.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.Ordinal);

    /// <summary>Os tokens canônicos (UPPER_SNAKE), para o CHECK de domínio e mensagens.</summary>
    public static readonly IReadOnlyList<string> TokensCanonicos = [.. ParaToken.Values];

    /// <summary>Token textual de contrato/banco (UPPER_SNAKE) de uma situação válida.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Se <paramref name="situacao"/> é o sentinela ou está fora do roster.</exception>
    public static string ParaTokenCanonico(SituacaoDaAprovacao situacao) =>
        ParaToken.TryGetValue(situacao, out string? token)
            ? token
            : throw new ArgumentOutOfRangeException(
                nameof(situacao), situacao, "Situação da aprovação da proteção fora do domínio fechado.");

    /// <summary>Resolve um token textual (UPPER_SNAKE) à situação; <see langword="false"/> quando inválido.</summary>
    public static bool TryAnalisar(string? token, out SituacaoDaAprovacao situacao)
    {
        if (!string.IsNullOrWhiteSpace(token) && DeToken.TryGetValue(token.Trim(), out SituacaoDaAprovacao resolvida))
        {
            situacao = resolvida;
            return true;
        }

        situacao = SituacaoDaAprovacao.Nenhuma;
        return false;
    }

    /// <summary>Resolve um token à sua enum (reidratação fail-fast do value converter).</summary>
    /// <exception cref="ArgumentOutOfRangeException">Se <paramref name="token"/> não é canônico.</exception>
    public static SituacaoDaAprovacao Analisar(string? token) =>
        TryAnalisar(token, out SituacaoDaAprovacao situacao)
            ? situacao
            : throw new ArgumentOutOfRangeException(
                nameof(token), token, "Token de situação da aprovação da proteção fora do domínio fechado.");
}
