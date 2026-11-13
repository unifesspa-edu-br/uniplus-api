namespace Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O relógio do host nos testes que precisam de um instante com a 7ª casa decimal e de o fazer andar até o limite exato
/// de uma expiração: segue o relógio do sistema até o teste parar o relógio num instante, e daí em diante devolve sempre
/// esse instante, tal como foi dado, sem truncar.
/// </summary>
internal sealed class RelogioControlado : TimeProvider
{
    private long _paradoEmTicks;

    public override DateTimeOffset GetUtcNow()
    {
        long parado = Interlocked.Read(ref _paradoEmTicks);
        return parado == 0 ? base.GetUtcNow() : new DateTimeOffset(parado, TimeSpan.Zero);
    }

    public void ParaNo(DateTimeOffset instante) => Interlocked.Exchange(ref _paradoEmTicks, instante.UtcTicks);

    public void Liberar() => Interlocked.Exchange(ref _paradoEmTicks, 0);
}
