namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Microsoft.Extensions.Hosting;

/// <summary>
/// Serviço hospedado que só conta as chamadas de <c>StartAsync</c>, para provar que um serviço registrado antes de outro
/// não chega a iniciar quando um <c>StartingAsync</c> falha. A contagem é segura para o início concorrente de serviços.
/// </summary>
internal sealed class SondaDeInicioDoHost : IHostedService
{
    private int _chamadasDeStartAsync;

    public int ChamadasDeStartAsync => Volatile.Read(ref _chamadasDeStartAsync);

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _chamadasDeStartAsync);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
