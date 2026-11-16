namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Messaging.SchemaRegistry;

using Microsoft.Extensions.Time.Testing;

/// <summary>
/// Relógio simulado que conta os temporizadores criados, para o teste avançar o tempo só depois de a espera existir e
/// aguardar o efeito do disparo, sem depender do escalonador.
/// </summary>
internal sealed class RelogioObservavel : FakeTimeProvider
{
    private static readonly TimeSpan Limite = TimeSpan.FromSeconds(10);

    private int _criados;
    private int _disparados;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        ITimer temporizador = base.CreateTimer(callback, state, dueTime, period);
        Interlocked.Increment(ref _criados);

        return temporizador;
    }

    /// <summary>Espera a repetição criar a espera e avança o relógio, sem chegar a disparar quando o intervalo não a completa.</summary>
    public async Task AdiantarAsync(TimeSpan intervalo)
    {
        await AguardarAsync(() => Volatile.Read(ref _criados) > _disparados).ConfigureAwait(false);
        Advance(intervalo);
    }

    /// <summary>Espera as esperas pendentes existirem, avança o relógio e espera cada uma delas tentar de novo.</summary>
    public async Task DispararAsync(TimeSpan intervalo, Func<int> tentativas, int pendentes = 1)
    {
        await AguardarAsync(() => Volatile.Read(ref _criados) - _disparados >= pendentes).ConfigureAwait(false);

        int antes = tentativas();
        Advance(intervalo);
        _disparados += pendentes;

        await AguardarAsync(() => tentativas() >= antes + pendentes).ConfigureAwait(false);
    }

    /// <summary>Avança o relógio quando nada deve acontecer; a folga dá chance a uma repetição indevida aparecer.</summary>
    public async Task AvancarSemEfeitoAsync(TimeSpan intervalo)
    {
        Advance(intervalo);
        await Task.Delay(TimeSpan.FromMilliseconds(50)).ConfigureAwait(false);
    }

    private static async Task AguardarAsync(Func<bool> condicao)
    {
        using CancellationTokenSource prazo = new(Limite);
        while (!condicao())
        {
            await Task.Delay(5, prazo.Token).ConfigureAwait(false);
        }
    }
}
