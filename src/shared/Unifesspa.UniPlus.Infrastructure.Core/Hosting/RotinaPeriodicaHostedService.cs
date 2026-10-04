namespace Unifesspa.UniPlus.Infrastructure.Core.Hosting;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Base das rotinas de manutenção que o processo executa em intervalo fixo, cada execução num
/// escopo de DI próprio — o <c>DbContext</c> e os repositórios do escopo não sobrevivem entre
/// execuções nem são compartilhados com requisições.
/// </summary>
/// <remarks>
/// <para>
/// A primeira execução acontece um intervalo depois da partida, não nela: a rotina não disputa
/// o boot com as migrations nem com o runtime de mensageria, e um host de teste que não a remove
/// não chega a executá-la. Os hosts de teste a removem pelo tipo base (<c>ApiFactoryBase</c>).
/// </para>
/// <para>
/// A falha de uma execução é registrada e a rotina segue para a próxima: banco ou armazenamento
/// indisponível por um momento não podem derrubar o processo que serve as requisições.
/// </para>
/// </remarks>
public abstract partial class RotinaPeriodicaHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;

    protected RotinaPeriodicaHostedService(IServiceScopeFactory scopeFactory, TimeProvider timeProvider, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>Nome da rotina nos logs.</summary>
    protected abstract string Nome { get; }

    /// <summary>
    /// Intervalo entre execuções. Uma execução mais longa que ele não sobrepõe a seguinte: os
    /// ciclos perdidos durante ela viram uma única execução logo depois.
    /// </summary>
    protected abstract TimeSpan Intervalo { get; }

    /// <summary>Uma execução da rotina, com os serviços do escopo criado para ela.</summary>
    protected abstract Task ExecutarAsync(IServiceProvider servicos, CancellationToken cancellationToken);

    protected sealed override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(Intervalo, _timeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                await ExecutarNoEscopoAsync(stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Parada do host: encerra sem tratar como falha.
        }
    }

    private async Task ExecutarNoEscopoAsync(CancellationToken stoppingToken)
    {
        try
        {
            AsyncServiceScope escopo = _scopeFactory.CreateAsyncScope();
            await using (escopo.ConfigureAwait(false))
            {
                await ExecutarAsync(escopo.ServiceProvider, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            LogExecucaoFalhou(_logger, Nome, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "A rotina periódica {Rotina} falhou; nova tentativa no próximo intervalo")]
    private static partial void LogExecucaoFalhou(ILogger logger, string rotina, Exception exception);
}
