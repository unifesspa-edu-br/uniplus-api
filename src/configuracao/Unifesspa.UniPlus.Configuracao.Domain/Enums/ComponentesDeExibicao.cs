namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Mapeamento entre <see cref="ComponenteDeExibicao"/> (PascalCase) e o token textual de contrato/banco
/// (UPPER_SNAKE), com parsing de domínio fechado por allowlist explícita (molde de
/// <see cref="FormatosTexto"/>). Fonte do CHECK de domínio em
/// <c>rol_de_fatos_candidato.componente_exibicao</c> e do value converter de persistência.
/// </summary>
public static class ComponentesDeExibicao
{
    private static readonly Dictionary<ComponenteDeExibicao, string> ParaToken = new()
    {
        [ComponenteDeExibicao.Moeda] = "MOEDA",
        [ComponenteDeExibicao.Situacao] = "SITUACAO",
    };

    private static readonly Dictionary<string, ComponenteDeExibicao> DeToken =
        ParaToken.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.Ordinal);

    /// <summary>Os tokens canônicos (UPPER_SNAKE), para o CHECK de domínio e mensagens.</summary>
    public static readonly IReadOnlyList<string> TokensCanonicos = [.. ParaToken.Values];

    /// <summary>Token textual de contrato/banco (UPPER_SNAKE) de um valor válido.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Se o valor é o sentinela ou está fora do roster.</exception>
    public static string ParaTokenCanonico(ComponenteDeExibicao valor) =>
        ParaToken.TryGetValue(valor, out string? token)
            ? token
            : throw new ArgumentOutOfRangeException(nameof(valor), valor, "Componente de exibição fora do domínio fechado.");

    /// <summary>Resolve um token textual (UPPER_SNAKE); <see langword="false"/> quando inválido.</summary>
    public static bool TryAnalisar(string? token, out ComponenteDeExibicao valor)
    {
        if (!string.IsNullOrWhiteSpace(token) && DeToken.TryGetValue(token.Trim(), out ComponenteDeExibicao resolvido))
        {
            valor = resolvido;
            return true;
        }

        valor = ComponenteDeExibicao.Nenhum;
        return false;
    }

    /// <summary>Resolve um token à sua enum (reidratação fail-fast do value converter).</summary>
    /// <exception cref="ArgumentOutOfRangeException">Se <paramref name="token"/> não é canônico.</exception>
    public static ComponenteDeExibicao Analisar(string? token) =>
        TryAnalisar(token, out ComponenteDeExibicao valor)
            ? valor
            : throw new ArgumentOutOfRangeException(nameof(token), token, "Token de componente de exibição fora do domínio fechado.");
}
