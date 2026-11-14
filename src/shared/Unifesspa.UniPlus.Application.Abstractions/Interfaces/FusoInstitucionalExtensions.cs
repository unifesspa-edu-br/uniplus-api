namespace Unifesspa.UniPlus.Application.Abstractions.Interfaces;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>O dia civil de um instante no fuso institucional.</summary>
public static class FusoInstitucionalExtensions
{
    /// <summary>
    /// O dia civil de <paramref name="instante"/> no fuso institucional, ou a recusa do resolvedor quando a base de
    /// fusos do runtime não o reconhece.
    /// </summary>
    public static Result<DateOnly> DiaCivilDe(this IResolvedorFusoInstitucional resolvedor, DateTimeOffset instante)
    {
        ArgumentNullException.ThrowIfNull(resolvedor);

        Result<TimeZoneInfo> fuso = resolvedor.Resolver();

        return fuso.IsFailure
            ? Result<DateOnly>.Failure(fuso.Error!)
            : Result<DateOnly>.Success(fuso.Value!.DiaCivilDe(instante));
    }

    /// <summary>O dia civil de <paramref name="instante"/> em <paramref name="fuso"/>.</summary>
    public static DateOnly DiaCivilDe(this TimeZoneInfo fuso, DateTimeOffset instante)
    {
        ArgumentNullException.ThrowIfNull(fuso);

        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instante, fuso).DateTime);
    }
}
