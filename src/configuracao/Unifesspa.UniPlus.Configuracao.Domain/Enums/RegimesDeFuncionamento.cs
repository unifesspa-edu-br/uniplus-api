namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Mapeamento entre <see cref="RegimeDeFuncionamento"/> (domínio, PascalCase) e
/// o código textual de contrato/banco (UPPER_SNAKE), com parsing de domínio
/// fechado.
/// </summary>
/// <remarks>
/// <para>O parsing é por <b>lista textual explícita de códigos aceitos</b> (<see cref="TryAnalisar"/>):
/// só os três códigos canônicos são aceitos — sem <c>Enum.TryParse</c>, que
/// aceitaria códigos numéricos e nomes PascalCase fora do contrato. Mesmo
/// expediente de <see cref="RegimesDeTurno"/>.</para>
/// <para>É o vocabulário fonte do CHECK de domínio em
/// <c>ofertas_de_curso.regime_de_funcionamento</c> (<see cref="TokensCanonicos"/>) e
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
        [RegimeDeFuncionamento.AlternanciaPedagogica] = "ALTERNANCIA_PEDAGOGICA",
    };

    private static readonly Dictionary<string, RegimeDeFuncionamento> DeToken =
        ParaToken.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.Ordinal);

    /// <summary>Os três códigos canônicos (UPPER_SNAKE), para o CHECK de domínio e mensagens.</summary>
    public static readonly IReadOnlyList<string> TokensCanonicos = [.. ParaToken.Values];

    /// <summary>Código textual de contrato/banco (UPPER_SNAKE) de um regime válido.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Se <paramref name="regime"/> é <see cref="RegimeDeFuncionamento.Nenhum"/> ou fora dos valores declarados.</exception>
    public static string ParaTokenCanonico(RegimeDeFuncionamento regime) =>
        ParaToken.TryGetValue(regime, out string? codigo)
            ? codigo
            : throw new ArgumentOutOfRangeException(
                nameof(regime), regime, "Regime de funcionamento fora do domínio fechado.");

    /// <summary>
    /// Resolve um código textual (UPPER_SNAKE) ao regime correspondente. Aceita
    /// <c>Trim</c>, mas é case-sensitive e rejeita códigos numéricos ou fora do
    /// domínio (lista de códigos aceitos). Retorna <see langword="false"/> quando inválido.
    /// </summary>
    public static bool TryAnalisar(string? codigo, out RegimeDeFuncionamento regime)
    {
        if (!string.IsNullOrWhiteSpace(codigo)
            && DeToken.TryGetValue(codigo.Trim(), out RegimeDeFuncionamento resolvido))
        {
            regime = resolvido;
            return true;
        }

        regime = RegimeDeFuncionamento.Nenhum;
        return false;
    }

    /// <summary>Indica se <paramref name="codigo"/> é um dos três códigos canônicos, sem alocar resultado.</summary>
    public static bool EhValido(string? codigo) => TryAnalisar(codigo, out _);

    /// <summary>
    /// Regime de turno que o regime de funcionamento <b>exige</b>, ou
    /// <see langword="null"/> quando não restringe:
    /// <see cref="RegimeDeFuncionamento.Intensivo"/> exige
    /// <see cref="RegimeDeTurno.Integral"/>;
    /// <see cref="RegimeDeFuncionamento.Extensivo"/> e <see cref="RegimeDeFuncionamento.AlternanciaPedagogica"/>
    /// aceita qualquer um dos regimes de turno vigentes (UNI-REQ-0138).
    /// </summary>
    /// <remarks>
    /// A exigência é conferida, nunca aplicada: quem declara INTENSIVO com regime
    /// de turno REGULAR recebe recusa, não promoção silenciosa a INTEGRAL.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Se <paramref name="regime"/> é <see cref="RegimeDeFuncionamento.Nenhum"/> ou fora dos valores declarados.</exception>
    public static RegimeDeTurno? RegimeDeTurnoExigido(RegimeDeFuncionamento regime) => regime switch
    {
        RegimeDeFuncionamento.Intensivo => RegimeDeTurno.Integral,
        RegimeDeFuncionamento.Extensivo => null,
        RegimeDeFuncionamento.AlternanciaPedagogica => null,
        _ => throw new ArgumentOutOfRangeException(
            nameof(regime), regime, "Regime de funcionamento fora do domínio fechado."),
    };
}
