namespace Unifesspa.UniPlus.Kernel.Extensions;

public static class DateTimeOffsetExtensions
{
    private static readonly TimeZoneInfo BrasiliaTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    public static DateTimeOffset ParaHorarioBrasilia(this DateTimeOffset dateTimeOffset) =>
        TimeZoneInfo.ConvertTime(dateTimeOffset, BrasiliaTimeZone);
}
