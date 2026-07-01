namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Mapeamento entre <see cref="ComposicaoVagas"/> (domínio, PascalCase) e o token
/// textual de contrato/banco (UPPER_SNAKE), com parsing de domínio fechado.
/// </summary>
/// <remarks>
/// <para>O parsing é por <b>allowlist textual explícita</b> (<see cref="TryAnalisar"/>):
/// só os quatro tokens canônicos são aceitos. Deliberadamente <b>não</b> usa
/// <c>Enum.TryParse</c>, que aceitaria tokens numéricos e nomes PascalCase do enum
/// — ambos fora do contrato textual da #589.</para>
/// <para>É o vocabulário fonte do CHECK de domínio em <c>modalidade.composicao_vagas</c>
/// (<see cref="TokensCanonicos"/>) e do value converter de persistência.</para>
/// </remarks>
public static class ComposicoesVagas
{
    private static readonly Dictionary<ComposicaoVagas, string> ParaToken = new()
    {
        [ComposicaoVagas.ResidualDoVo] = "RESIDUAL_DO_VO",
        [ComposicaoVagas.DentroDoVr] = "DENTRO_DO_VR",
        [ComposicaoVagas.RetiraDe] = "RETIRA_DE",
        [ComposicaoVagas.SuplementarAoTotal] = "SUPLEMENTAR_AO_TOTAL",
    };

    private static readonly Dictionary<string, ComposicaoVagas> DeToken =
        ParaToken.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.Ordinal);

    /// <summary>Os quatro tokens canônicos (UPPER_SNAKE), para o CHECK de domínio e mensagens.</summary>
    public static readonly IReadOnlyList<string> TokensCanonicos = [.. ParaToken.Values];

    /// <summary>Token textual de contrato/banco (UPPER_SNAKE) de uma composição válida.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Se <paramref name="composicao"/> é <see cref="ComposicaoVagas.Nenhuma"/> ou fora do roster.</exception>
    public static string ParaTokenCanonico(ComposicaoVagas composicao) =>
        ParaToken.TryGetValue(composicao, out string? token)
            ? token
            : throw new ArgumentOutOfRangeException(
                nameof(composicao), composicao, "Composição de vagas fora do domínio fechado.");

    /// <summary>
    /// Resolve um token textual (UPPER_SNAKE) à composição correspondente. Aceita
    /// <c>Trim</c>, mas é case-sensitive e rejeita tokens numéricos ou fora do
    /// domínio (allowlist). Retorna <see langword="false"/> quando inválido.
    /// </summary>
    public static bool TryAnalisar(string? token, out ComposicaoVagas composicao)
    {
        if (!string.IsNullOrWhiteSpace(token)
            && DeToken.TryGetValue(token.Trim(), out ComposicaoVagas resolvida))
        {
            composicao = resolvida;
            return true;
        }

        composicao = ComposicaoVagas.Nenhuma;
        return false;
    }

    /// <summary>Indica se <paramref name="token"/> é um dos quatro tokens canônicos, sem alocar resultado.</summary>
    public static bool EhValido(string? token) => TryAnalisar(token, out _);
}
