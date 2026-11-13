namespace Unifesspa.UniPlus.Kernel.Extensions;

public static class TimeSpanExtensions
{
    /// <summary>
    /// Descarta os ticks abaixo de um microssegundo, sem arredondar: a duração fica em microssegundos inteiros
    /// e, somada a um instante já em microssegundos, não reintroduz a 7ª casa decimal.
    /// </summary>
    public static TimeSpan TruncadoEmMicrossegundos(this TimeSpan valor) =>
        TimeSpan.FromTicks(valor.Ticks - (valor.Ticks % TimeSpan.TicksPerMicrosecond));
}
