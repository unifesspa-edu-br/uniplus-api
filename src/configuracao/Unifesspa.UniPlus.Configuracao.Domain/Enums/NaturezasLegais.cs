namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Mapeamento entre <see cref="NaturezaLegal"/> (domínio, PascalCase) e o código
/// textual de contrato/banco (UPPER_SNAKE), com parsing de domínio fechado.
/// </summary>
/// <remarks>
/// <para>O parsing é por <b>lista textual explícita de códigos aceitos</b> (<see cref="TryAnalisar"/>):
/// só os três códigos canônicos são aceitos. Deliberadamente <b>não</b> usa
/// <c>Enum.TryParse</c>, que aceitaria códigos numéricos (<c>"1"</c> → primeiro
/// valor) e nomes PascalCase do enum — ambos fora do contrato textual da #589.</para>
/// <para>É o vocabulário fonte do CHECK de domínio em <c>modalidade.natureza_legal</c>
/// (<see cref="TokensCanonicos"/>) e do value converter de persistência.</para>
/// </remarks>
public static class NaturezasLegais
{
    private static readonly Dictionary<NaturezaLegal, string> ParaToken = new()
    {
        [NaturezaLegal.CotaReservada] = "COTA_RESERVADA",
        [NaturezaLegal.Ampla] = "AMPLA",
        [NaturezaLegal.AcaoAfirmativa] = "ACAO_AFIRMATIVA",
    };

    private static readonly Dictionary<string, NaturezaLegal> DeToken =
        ParaToken.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.Ordinal);

    /// <summary>Os três códigos canônicos (UPPER_SNAKE), para o CHECK de domínio e mensagens.</summary>
    public static readonly IReadOnlyList<string> TokensCanonicos = [.. ParaToken.Values];

    /// <summary>Código textual de contrato/banco (UPPER_SNAKE) de uma natureza válida.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Se <paramref name="natureza"/> é <see cref="NaturezaLegal.Nenhuma"/> ou fora dos valores declarados.</exception>
    public static string ParaTokenCanonico(NaturezaLegal natureza) =>
        ParaToken.TryGetValue(natureza, out string? codigo)
            ? codigo
            : throw new ArgumentOutOfRangeException(
                nameof(natureza), natureza, "Natureza legal fora do domínio fechado.");

    /// <summary>
    /// Resolve um código textual (UPPER_SNAKE) à natureza correspondente. Aceita
    /// <c>Trim</c>, mas é case-sensitive e rejeita códigos numéricos ou fora do
    /// domínio (lista de códigos aceitos). Retorna <see langword="false"/> quando inválido.
    /// </summary>
    public static bool TryAnalisar(string? codigo, out NaturezaLegal natureza)
    {
        if (!string.IsNullOrWhiteSpace(codigo)
            && DeToken.TryGetValue(codigo.Trim(), out NaturezaLegal resolvida))
        {
            natureza = resolvida;
            return true;
        }

        natureza = NaturezaLegal.Nenhuma;
        return false;
    }

    /// <summary>Indica se <paramref name="codigo"/> é um dos três códigos canônicos, sem alocar resultado.</summary>
    public static bool EhValido(string? codigo) => TryAnalisar(codigo, out _);
}
