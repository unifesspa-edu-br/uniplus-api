namespace Unifesspa.UniPlus.Kernel.UnitTests.Tempo;

internal sealed class FonteDeRelogioDeTeste : TimeProvider
{
    public const long FrequenciaDeTimestamp = 12_345;
    public const long Timestamp = 987_654;

    public DateTimeOffset Agora { get; set; }

    public TimeZoneInfo FusoLocal { get; init; } = TimeZoneInfo.Utc;

    public ITimer TemporizadorCriado { get; } = new TemporizadorDeTeste();

    public (TimeSpan DueTime, TimeSpan Period)? ParametrosDoTemporizador { get; private set; }

    public override DateTimeOffset GetUtcNow() => Agora;

    public override long GetTimestamp() => Timestamp;

    public override long TimestampFrequency => FrequenciaDeTimestamp;

    public override TimeZoneInfo LocalTimeZone => FusoLocal;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        ParametrosDoTemporizador = (dueTime, period);
        return TemporizadorCriado;
    }

    private sealed class TemporizadorDeTeste : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
