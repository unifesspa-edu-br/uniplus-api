namespace Unifesspa.UniPlus.Infrastructure.Core.Pagination;

/// <summary>
/// Resultado da decodificação de um cursor: status discriminante e — somente
/// quando <see cref="CursorDecodeStatus.Success"/> — o <see cref="CursorPayload"/>
/// recuperado.
/// </summary>
public sealed record CursorDecodeResult(CursorDecodeStatus Status, CursorPayload? Payload)
{
    public static CursorDecodeResult Success(CursorPayload payload) => new(CursorDecodeStatus.Success, payload);

    public static CursorDecodeResult Invalid() => new(CursorDecodeStatus.Invalid, Payload: null);

    public static CursorDecodeResult Expired(CursorPayload payload) => new(CursorDecodeStatus.Expired, payload);
}
