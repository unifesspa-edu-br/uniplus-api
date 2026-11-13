namespace Unifesspa.UniPlus.Kernel.Extensions;

public static class DateTimeOffsetExtensions
{
    private static readonly TimeZoneInfo BrasiliaTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    public static DateTimeOffset ParaHorarioBrasilia(this DateTimeOffset dateTimeOffset) =>
        TimeZoneInfo.ConvertTime(dateTimeOffset, BrasiliaTimeZone);

    /// <summary>
    /// Descarta os ticks abaixo de um microssegundo (a 7ª casa decimal), sem arredondar,
    /// preservando o offset. É a precisão do <c>timestamptz</c> do PostgreSQL.
    /// </summary>
    public static DateTimeOffset TruncadoEmMicrossegundos(this DateTimeOffset valor) =>
        new(valor.Ticks - (valor.Ticks % TimeSpan.TicksPerMicrosecond), valor.Offset);
}
