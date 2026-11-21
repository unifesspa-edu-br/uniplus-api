namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Cardinalidade que cada <see cref="RegimeDeTurno"/> impõe à coleção de turnos, fonte única consumida pela invariante do
/// agregado e pelo CHECK que a espelha no banco.
/// </summary>
public static class RegimesDeTurno
{
    /// <summary>
    /// Quantidade exata de turnos distintos que o regime exige: um para <see cref="RegimeDeTurno.Regular"/>, dois para
    /// <see cref="RegimeDeTurno.Integral"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Se <paramref name="regime"/> não é um valor declarado.</exception>
    public static int TurnosExigidos(RegimeDeTurno regime) => regime switch
    {
        RegimeDeTurno.Regular => 1,
        RegimeDeTurno.Integral => 2,
        _ => throw new ArgumentOutOfRangeException(
            nameof(regime), regime, "Regime de turno fora do domínio fechado."),
    };
}
