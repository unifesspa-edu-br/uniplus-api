namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using Microsoft.Extensions.DependencyInjection;

/// <summary>O escopo que avisa quando a passada da rotina termina.</summary>
internal sealed class EscopoQueAvisaOFim(IServiceScopeFactory origem, TaskCompletionSource passada) : IServiceScopeFactory
{
    public IServiceScope CreateScope() => new Escopo(origem.CreateScope(), passada);

    private sealed class Escopo(IServiceScope interno, TaskCompletionSource passada) : IServiceScope
    {
        public IServiceProvider ServiceProvider => interno.ServiceProvider;

        public void Dispose()
        {
            interno.Dispose();
            passada.TrySetResult();
        }
    }
}

/// <summary>O relógio cujo temporizador o teste dispara à mão, uma vez.</summary>
internal sealed class RelogioComTemporizadorManual : TimeProvider
{
    private readonly TaskCompletionSource<(TimerCallback Callback, object? State)> _armado = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public async Task DispararAsync()
    {
        (TimerCallback callback, object? state) = await _armado.Task;
        callback(state);
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        _armado.TrySetResult((callback, state));

        return new TemporizadorInerte();
    }

    private sealed class TemporizadorInerte : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
