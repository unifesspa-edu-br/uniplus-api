namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Mapeamento entre <see cref="AcaoQuandoIndeferido"/> (domínio, PascalCase) e o
/// token textual de contrato/banco (UPPER_SNAKE), com parsing de domínio fechado.
/// </summary>
/// <remarks>
/// <para>O parsing é por <b>allowlist textual explícita</b> (<see cref="TryAnalisar"/>):
/// só os dois tokens canônicos são aceitos. Deliberadamente <b>não</b> usa
/// <c>Enum.TryParse</c>, que aceitaria tokens numéricos e nomes PascalCase do enum
/// — ambos fora do contrato textual da #589.</para>
/// <para>É o vocabulário fonte do CHECK de domínio (null-safe) em
/// <c>modalidade.acao_quando_indeferido</c> (<see cref="TokensCanonicos"/>) e do
/// value converter de persistência.</para>
/// </remarks>
public static class AcoesQuandoIndeferido
{
    private static readonly Dictionary<AcaoQuandoIndeferido, string> ParaToken = new()
    {
        [AcaoQuandoIndeferido.ReclassificarAc] = "RECLASSIFICAR_AC",
        [AcaoQuandoIndeferido.ReclassificarRegraEdital] = "RECLASSIFICAR_REGRA_EDITAL",
    };

    private static readonly Dictionary<string, AcaoQuandoIndeferido> DeToken =
        ParaToken.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.Ordinal);

    /// <summary>Os dois tokens canônicos (UPPER_SNAKE), para o CHECK de domínio e mensagens.</summary>
    public static readonly IReadOnlyList<string> TokensCanonicos = [.. ParaToken.Values];

    /// <summary>Token textual de contrato/banco (UPPER_SNAKE) de uma ação válida.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Se <paramref name="acao"/> é <see cref="AcaoQuandoIndeferido.Nenhuma"/> ou fora do roster.</exception>
    public static string ParaTokenCanonico(AcaoQuandoIndeferido acao) =>
        ParaToken.TryGetValue(acao, out string? token)
            ? token
            : throw new ArgumentOutOfRangeException(
                nameof(acao), acao, "Ação quando indeferido fora do domínio fechado.");

    /// <summary>
    /// Resolve um token textual (UPPER_SNAKE) à ação correspondente. Aceita
    /// <c>Trim</c>, mas é case-sensitive e rejeita tokens numéricos ou fora do
    /// domínio (allowlist). Retorna <see langword="false"/> quando inválido.
    /// </summary>
    public static bool TryAnalisar(string? token, out AcaoQuandoIndeferido acao)
    {
        if (!string.IsNullOrWhiteSpace(token)
            && DeToken.TryGetValue(token.Trim(), out AcaoQuandoIndeferido resolvida))
        {
            acao = resolvida;
            return true;
        }

        acao = AcaoQuandoIndeferido.Nenhuma;
        return false;
    }

    /// <summary>Indica se <paramref name="token"/> é um dos dois tokens canônicos, sem alocar resultado.</summary>
    public static bool EhValido(string? token) => TryAnalisar(token, out _);
}
