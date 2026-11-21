namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Mapeamento entre <see cref="RegimeDeTurno"/> (domínio, PascalCase) e o código
/// textual de contrato/banco (UPPER_SNAKE), com parsing de domínio fechado.
/// </summary>
/// <remarks>
/// <para>O parsing é por <b>lista textual explícita de códigos aceitos</b> (<see cref="TryAnalisar"/>):
/// só os dois códigos canônicos são aceitos — sem <c>Enum.TryParse</c>, que
/// aceitaria códigos numéricos e nomes PascalCase fora do contrato.</para>
/// <para>É o vocabulário fonte do CHECK de domínio em
/// <c>ofertas_de_curso.regime_de_turno</c> (<see cref="TokensCanonicos"/>) e do value
/// converter de persistência. <see cref="TurnosExigidos"/> é a fonte única da
/// cardinalidade que cada regime impõe à coleção de turnos.</para>
/// </remarks>
public static class RegimesDeTurno
{
    private static readonly Dictionary<RegimeDeTurno, string> ParaToken = new()
    {
        [RegimeDeTurno.Regular] = "REGULAR",
        [RegimeDeTurno.Integral] = "INTEGRAL",
    };

    private static readonly Dictionary<string, RegimeDeTurno> DeToken =
        ParaToken.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.Ordinal);

    /// <summary>Os dois códigos canônicos (UPPER_SNAKE), para o CHECK de domínio e mensagens.</summary>
    public static readonly IReadOnlyList<string> TokensCanonicos = [.. ParaToken.Values];

    /// <summary>Código textual de contrato/banco (UPPER_SNAKE) de um regime válido.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Se <paramref name="regime"/> é <see cref="RegimeDeTurno.Nenhum"/> ou fora dos valores declarados.</exception>
    public static string ParaTokenCanonico(RegimeDeTurno regime) =>
        ParaToken.TryGetValue(regime, out string? codigo)
            ? codigo
            : throw new ArgumentOutOfRangeException(
                nameof(regime), regime, "Regime de turno fora do domínio fechado.");

    /// <summary>
    /// Resolve um código textual (UPPER_SNAKE) ao regime correspondente. Aceita
    /// <c>Trim</c>, mas é case-sensitive e rejeita códigos numéricos ou fora do
    /// domínio (lista de códigos aceitos). Retorna <see langword="false"/> quando inválido.
    /// </summary>
    public static bool TryAnalisar(string? codigo, out RegimeDeTurno regime)
    {
        if (!string.IsNullOrWhiteSpace(codigo)
            && DeToken.TryGetValue(codigo.Trim(), out RegimeDeTurno resolvido))
        {
            regime = resolvido;
            return true;
        }

        regime = RegimeDeTurno.Nenhum;
        return false;
    }

    /// <summary>Indica se <paramref name="codigo"/> é um dos dois códigos canônicos, sem alocar resultado.</summary>
    public static bool EhValido(string? codigo) => TryAnalisar(codigo, out _);

    /// <summary>
    /// Quantidade exata de turnos distintos que o regime exige: um para
    /// <see cref="RegimeDeTurno.Regular"/>, dois para
    /// <see cref="RegimeDeTurno.Integral"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Se <paramref name="regime"/> é <see cref="RegimeDeTurno.Nenhum"/> ou fora dos valores declarados.</exception>
    public static int TurnosExigidos(RegimeDeTurno regime) => regime switch
    {
        RegimeDeTurno.Regular => 1,
        RegimeDeTurno.Integral => 2,
        _ => throw new ArgumentOutOfRangeException(
            nameof(regime), regime, "Regime de turno fora do domínio fechado."),
    };
}
