namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Mapeamento entre <see cref="Abrangencia"/> (domínio, PascalCase) e o código
/// textual de contrato/banco (UPPER_SNAKE), com parsing de domínio fechado.
/// </summary>
/// <remarks>
/// Parsing por <b>lista textual explícita de códigos aceitos</b> (<see cref="TryAnalisar"/>) — não usa
/// <c>Enum.TryParse</c>, que aceitaria códigos numéricos e nomes PascalCase fora do
/// contrato textual.
/// </remarks>
public static class Abrangencias
{
    private static readonly Dictionary<Abrangencia, string> ParaToken = new()
    {
        [Abrangencia.Nacional] = "NACIONAL",
        [Abrangencia.Estadual] = "ESTADUAL",
        [Abrangencia.Municipal] = "MUNICIPAL",
        [Abrangencia.Institucional] = "INSTITUCIONAL",
    };

    private static readonly Dictionary<string, Abrangencia> DeToken =
        ParaToken.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.Ordinal);

    /// <summary>Os quatro códigos canônicos (UPPER_SNAKE), para o CHECK de domínio e mensagens.</summary>
    public static readonly IReadOnlyList<string> TokensCanonicos = [.. ParaToken.Values];

    /// <summary>Código textual de contrato/banco (UPPER_SNAKE) de uma abrangência válida.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Se <paramref name="abrangencia"/> é <see cref="Abrangencia.Nenhuma"/> ou fora dos valores declarados.</exception>
    public static string ParaTokenCanonico(Abrangencia abrangencia) =>
        ParaToken.TryGetValue(abrangencia, out string? codigo)
            ? codigo
            : throw new ArgumentOutOfRangeException(
                nameof(abrangencia), abrangencia, "Abrangência fora do domínio fechado.");

    /// <summary>
    /// Resolve um código textual (UPPER_SNAKE) à abrangência correspondente. Aceita
    /// <c>Trim</c>, mas é case-sensitive e rejeita códigos fora do domínio (lista de códigos aceitos).
    /// Retorna <see langword="false"/> quando inválido.
    /// </summary>
    public static bool TryAnalisar(string? codigo, out Abrangencia abrangencia)
    {
        if (!string.IsNullOrWhiteSpace(codigo)
            && DeToken.TryGetValue(codigo.Trim(), out Abrangencia resolvida))
        {
            abrangencia = resolvida;
            return true;
        }

        abrangencia = Abrangencia.Nenhuma;
        return false;
    }

    /// <summary>Indica se <paramref name="codigo"/> é um dos códigos canônicos, sem alocar resultado.</summary>
    public static bool EhValido(string? codigo) => TryAnalisar(codigo, out _);
}
