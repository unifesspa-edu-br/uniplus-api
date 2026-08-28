namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Mapeamento entre <see cref="RegimeDeFuncionamento"/> (domínio, PascalCase) e
/// o token textual de contrato/banco (UPPER_SNAKE), com parsing de domínio
/// fechado.
/// </summary>
/// <remarks>
/// <para>O parsing é por <b>allowlist textual explícita</b> (<see cref="TryAnalisar"/>):
/// só os dois tokens canônicos são aceitos — sem <c>Enum.TryParse</c>, que
/// aceitaria tokens numéricos e nomes PascalCase fora do contrato. Mesmo
/// expediente de <see cref="RegimesDeTurno"/>.</para>
/// <para>É o vocabulário fonte do CHECK de domínio em
/// <c>oferta_curso.regime_de_funcionamento</c> (<see cref="TokensCanonicos"/>) e
/// do value converter de persistência. <see cref="RegimeDeTurnoExigido"/> é a
/// fonte única da compatibilidade entre as duas dimensões — consumida pela
/// invariante do agregado e pelo CHECK que a espelha no banco.</para>
/// </remarks>
public static class RegimesDeFuncionamento
{
    private static readonly Dictionary<RegimeDeFuncionamento, string> ParaToken = new()
    {
        [RegimeDeFuncionamento.Intensivo] = "INTENSIVO",
        [RegimeDeFuncionamento.Extensivo] = "EXTENSIVO",
    };

    private static readonly Dictionary<string, RegimeDeFuncionamento> DeToken =
        ParaToken.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.Ordinal);

    /// <summary>Os dois tokens canônicos (UPPER_SNAKE), para o CHECK de domínio e mensagens.</summary>
    public static readonly IReadOnlyList<string> TokensCanonicos = [.. ParaToken.Values];

    /// <summary>Token textual de contrato/banco (UPPER_SNAKE) de um regime válido.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Se <paramref name="regime"/> é <see cref="RegimeDeFuncionamento.Nenhum"/> ou fora do roster.</exception>
    public static string ParaTokenCanonico(RegimeDeFuncionamento regime) =>
        ParaToken.TryGetValue(regime, out string? token)
            ? token
            : throw new ArgumentOutOfRangeException(
                nameof(regime), regime, "Regime de funcionamento fora do domínio fechado.");

    /// <summary>
    /// Resolve um token textual (UPPER_SNAKE) ao regime correspondente. Aceita
    /// <c>Trim</c>, mas é case-sensitive e rejeita tokens numéricos ou fora do
    /// domínio (allowlist). Retorna <see langword="false"/> quando inválido.
    /// </summary>
    public static bool TryAnalisar(string? token, out RegimeDeFuncionamento regime)
    {
        if (!string.IsNullOrWhiteSpace(token)
            && DeToken.TryGetValue(token.Trim(), out RegimeDeFuncionamento resolvido))
        {
            regime = resolvido;
            return true;
        }

        regime = RegimeDeFuncionamento.Nenhum;
        return false;
    }

    /// <summary>Indica se <paramref name="token"/> é um dos dois tokens canônicos, sem alocar resultado.</summary>
    public static bool EhValido(string? token) => TryAnalisar(token, out _);

    /// <summary>
    /// Regime de turno que o regime de funcionamento <b>exige</b>, ou
    /// <see langword="null"/> quando não restringe:
    /// <see cref="RegimeDeFuncionamento.Intensivo"/> exige
    /// <see cref="RegimeDeTurno.Integral"/>;
    /// <see cref="RegimeDeFuncionamento.Extensivo"/> aceita qualquer um dos
    /// regimes de turno vigentes (UNI-REQ-0138).
    /// </summary>
    /// <remarks>
    /// A exigência é conferida, nunca aplicada: quem declara INTENSIVO com regime
    /// de turno REGULAR recebe recusa, não promoção silenciosa a INTEGRAL.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Se <paramref name="regime"/> é <see cref="RegimeDeFuncionamento.Nenhum"/> ou fora do roster.</exception>
    public static RegimeDeTurno? RegimeDeTurnoExigido(RegimeDeFuncionamento regime) => regime switch
    {
        RegimeDeFuncionamento.Intensivo => RegimeDeTurno.Integral,
        RegimeDeFuncionamento.Extensivo => null,
        _ => throw new ArgumentOutOfRangeException(
            nameof(regime), regime, "Regime de funcionamento fora do domínio fechado."),
    };
}
