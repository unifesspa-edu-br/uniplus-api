namespace Unifesspa.UniPlus.Kernel.Tempo;

using Unifesspa.UniPlus.Kernel.Extensions;

/// <summary>
/// Relógio que devolve o instante truncado em microssegundos, a mesma precisão que o
/// banco persiste, de modo que o valor gerado em memória é idêntico ao valor relido.
/// Os demais membros repassam à fonte.
/// </summary>
public sealed class RelogioEmMicrossegundos : TimeProvider
{
    private readonly TimeProvider _fonte;

    public RelogioEmMicrossegundos(TimeProvider fonte)
    {
        ArgumentNullException.ThrowIfNull(fonte);
        _fonte = fonte;
    }

    /// <summary>Relógio do sistema em microssegundos; é o único uso de <see cref="TimeProvider.System"/> em produção.</summary>
    public static RelogioEmMicrossegundos DoSistema { get; } = new(TimeProvider.System);

    public override DateTimeOffset GetUtcNow() => _fonte.GetUtcNow().TruncadoEmMicrossegundos();

    public override long GetTimestamp() => _fonte.GetTimestamp();

    public override long TimestampFrequency => _fonte.TimestampFrequency;

    public override TimeZoneInfo LocalTimeZone => _fonte.LocalTimeZone;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
        _fonte.CreateTimer(callback, state, dueTime, period);
}
