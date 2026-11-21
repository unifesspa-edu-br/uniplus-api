namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Regra de compatibilidade entre o <see cref="RegimeDeFuncionamento"/> e o <see cref="RegimeDeTurno"/>, fonte única
/// consumida pela invariante do agregado e pelo CHECK que a espelha no banco.
/// </summary>
public static class RegimesDeFuncionamento
{
    /// <summary>
    /// Regime de turno que o regime de funcionamento <b>exige</b>, ou <see langword="null"/> quando não restringe:
    /// <see cref="RegimeDeFuncionamento.Intensivo"/> exige <see cref="RegimeDeTurno.Integral"/>;
    /// <see cref="RegimeDeFuncionamento.Extensivo"/> e <see cref="RegimeDeFuncionamento.AlternanciaPedagogica"/> aceitam
    /// qualquer um dos regimes de turno vigentes (UNI-REQ-0138).
    /// </summary>
    /// <remarks>
    /// A exigência é conferida, nunca aplicada: quem declara INTENSIVO com regime de turno REGULAR recebe recusa, não
    /// promoção silenciosa a INTEGRAL.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Se <paramref name="regime"/> não é um valor declarado.</exception>
    public static RegimeDeTurno? RegimeDeTurnoExigido(RegimeDeFuncionamento regime) => regime switch
    {
        RegimeDeFuncionamento.Intensivo => RegimeDeTurno.Integral,
        RegimeDeFuncionamento.Extensivo => null,
        RegimeDeFuncionamento.AlternanciaPedagogica => null,
        _ => throw new ArgumentOutOfRangeException(
            nameof(regime), regime, "Regime de funcionamento fora do domínio fechado."),
    };
}
