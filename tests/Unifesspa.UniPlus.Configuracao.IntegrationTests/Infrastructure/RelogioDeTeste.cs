namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

/// <summary>
/// O relógio do host nos testes da Configuração: segue o relógio do sistema e, quando o teste o congela, devolve sempre o
/// mesmo instante, para reproduzir escritas que o relógio não distingue.
/// </summary>
internal sealed class RelogioDeTeste : TimeProvider
{
    private long _congeladoEmTicks;

    public override DateTimeOffset GetUtcNow()
    {
        long congelado = Interlocked.Read(ref _congeladoEmTicks);
        return congelado == 0 ? base.GetUtcNow() : new DateTimeOffset(congelado, TimeSpan.Zero);
    }

    /// <summary>
    /// Congela no instante corrente, cortado no segundo: o banco guarda microssegundos, e o instante relido tem de ser
    /// igual ao que o relógio volta a dar. O relógio volta a andar quando o retorno é descartado.
    /// </summary>
    public IDisposable Congelar()
    {
        DateTimeOffset agora = base.GetUtcNow();
        Interlocked.Exchange(ref _congeladoEmTicks, agora.UtcTicks - (agora.UtcTicks % TimeSpan.TicksPerSecond));
        return new Descongelamento(this);
    }

    private sealed class Descongelamento(RelogioDeTeste relogio) : IDisposable
    {
        public void Dispose() => Interlocked.Exchange(ref relogio._congeladoEmTicks, 0);
    }
}
