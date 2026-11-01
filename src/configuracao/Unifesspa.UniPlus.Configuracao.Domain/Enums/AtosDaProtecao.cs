namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Mapeamento entre <see cref="AtoDaProtecao"/> e o token textual de contrato/banco (UPPER_SNAKE), com
/// parsing de domínio fechado por allowlist explícita (molde de <see cref="SituacoesDaAprovacao"/>).
/// </summary>
public static class AtosDaProtecao
{
    private static readonly Dictionary<AtoDaProtecao, string> ParaToken = new()
    {
        [AtoDaProtecao.Solicitacao] = "SOLICITACAO",
        [AtoDaProtecao.Aprovacao] = "APROVACAO",
        [AtoDaProtecao.AjusteRecomendado] = "AJUSTE_RECOMENDADO",
        [AtoDaProtecao.Reprovacao] = "REPROVACAO",
        [AtoDaProtecao.Aceite] = "ACEITE",
    };

    private static readonly Dictionary<string, AtoDaProtecao> DeToken =
        ParaToken.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.Ordinal);

    /// <summary>Os atos que são decisões da proteção de dados: aprovação, ajuste recomendado e reprovação.</summary>
    public static readonly IReadOnlyList<AtoDaProtecao> Decisoes =
        [AtoDaProtecao.Aprovacao, AtoDaProtecao.AjusteRecomendado, AtoDaProtecao.Reprovacao];

    /// <summary>Os tokens canônicos (UPPER_SNAKE), para o CHECK de domínio e mensagens.</summary>
    public static readonly IReadOnlyList<string> TokensCanonicos = [.. ParaToken.Values];

    /// <summary>Token textual de contrato/banco de um ato válido.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Se <paramref name="ato"/> é o sentinela ou está fora do roster.</exception>
    public static string ParaTokenCanonico(AtoDaProtecao ato) =>
        ParaToken.TryGetValue(ato, out string? token)
            ? token
            : throw new ArgumentOutOfRangeException(nameof(ato), ato, "Ato da proteção fora do domínio fechado.");

    /// <summary>Resolve um token textual ao ato; <see langword="false"/> quando inválido.</summary>
    public static bool TryAnalisar(string? token, out AtoDaProtecao ato)
    {
        if (!string.IsNullOrWhiteSpace(token) && DeToken.TryGetValue(token.Trim(), out AtoDaProtecao resolvido))
        {
            ato = resolvido;
            return true;
        }

        ato = AtoDaProtecao.Nenhum;
        return false;
    }

    /// <summary>Resolve um token ao seu enum (reidratação fail-fast do value converter).</summary>
    /// <exception cref="ArgumentOutOfRangeException">Se <paramref name="token"/> não é canônico.</exception>
    public static AtoDaProtecao Analisar(string? token) =>
        TryAnalisar(token, out AtoDaProtecao ato)
            ? ato
            : throw new ArgumentOutOfRangeException(nameof(token), token, "Token de ato da proteção fora do domínio fechado.");
}
