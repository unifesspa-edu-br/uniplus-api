namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

/// <summary>Relógio que acompanha o instante real e só anda além dele quando o teste o avança.</summary>
internal sealed class RelogioAvancavelDeTeste : TimeProvider
{
    private long _avancoEmTicks;

    public override DateTimeOffset GetUtcNow() =>
        base.GetUtcNow() + TimeSpan.FromTicks(Interlocked.Read(ref _avancoEmTicks));

    public void Reiniciar() => Interlocked.Exchange(ref _avancoEmTicks, 0);

    public void Avancar(TimeSpan intervalo) => Interlocked.Add(ref _avancoEmTicks, intervalo.Ticks);
}
