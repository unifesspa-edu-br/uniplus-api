namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Mapeamento entre <see cref="OrigemDataFase"/> (domínio, PascalCase) e o código
/// textual de contrato/banco (UPPER_SNAKE), com parsing de domínio fechado.
/// </summary>
/// <remarks>
/// <para>O parsing é por <b>lista textual explícita de códigos aceitos</b> (<see cref="TryAnalisar"/>):
/// só os dois códigos canônicos são aceitos. Deliberadamente <b>não</b> usa
/// <c>Enum.TryParse</c>, que aceitaria códigos numéricos e nomes PascalCase do enum.</para>
/// <para>É o vocabulário fonte do CHECK de domínio em <c>fases_canonicas.origem_data</c>
/// (<see cref="TokensCanonicos"/>) e do value converter de persistência.</para>
/// </remarks>
public static class OrigensDataFase
{
    private static readonly Dictionary<OrigemDataFase, string> ParaToken = new()
    {
        [OrigemDataFase.NoEdital] = "NO_EDITAL",
        [OrigemDataFase.NaExecucao] = "NA_EXECUCAO",
    };

    private static readonly Dictionary<string, OrigemDataFase> DeToken =
        ParaToken.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.Ordinal);

    /// <summary>Os dois códigos canônicos (UPPER_SNAKE), para o CHECK de domínio e mensagens.</summary>
    public static readonly IReadOnlyList<string> TokensCanonicos = [.. ParaToken.Values];

    /// <summary>Código textual de contrato/banco (UPPER_SNAKE) de uma origem de data válida.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Se <paramref name="origem"/> é <see cref="OrigemDataFase.Nenhuma"/> ou fora dos valores declarados.</exception>
    public static string ParaTokenCanonico(OrigemDataFase origem) =>
        ParaToken.TryGetValue(origem, out string? codigo)
            ? codigo
            : throw new ArgumentOutOfRangeException(
                nameof(origem), origem, "Origem da data fora do domínio fechado.");

    /// <summary>
    /// Resolve um código textual (UPPER_SNAKE) à origem correspondente. Aceita
    /// <c>Trim</c>, mas é case-sensitive e rejeita códigos numéricos ou fora do
    /// domínio (lista de códigos aceitos). Retorna <see langword="false"/> quando inválido.
    /// </summary>
    public static bool TryAnalisar(string? codigo, out OrigemDataFase origem)
    {
        if (!string.IsNullOrWhiteSpace(codigo)
            && DeToken.TryGetValue(codigo.Trim(), out OrigemDataFase resolvida))
        {
            origem = resolvida;
            return true;
        }

        origem = OrigemDataFase.Nenhuma;
        return false;
    }

    /// <summary>Indica se <paramref name="codigo"/> é um dos dois códigos canônicos, sem alocar resultado.</summary>
    public static bool EhValido(string? codigo) => TryAnalisar(codigo, out _);
}
