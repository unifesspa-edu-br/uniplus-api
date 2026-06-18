namespace Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Etl;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Consome a fila de disparos do ETL (Story #674) e executa cada carga num escopo de DI
/// próprio (o orquestrador e o <c>GeoDbContext</c> são scoped). No startup, reconcilia
/// execuções <c>EmAndamento</c> abandonadas por crash/restart anterior — marca-as
/// <c>Falhou</c> antes de aceitar novos disparos, senão o índice único parcial as deixaria
/// bloqueando todos os 409 indefinidamente.
/// </summary>
internal sealed partial class GeoImportacaoBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IGeoImportacaoFila _fila;
    private readonly TimeProvider _relogio;
    private readonly TimeSpan _limiteAbandono;
    private readonly ILogger<GeoImportacaoBackgroundService> _logger;

    public GeoImportacaoBackgroundService(
        IServiceScopeFactory scopeFactory,
        IGeoImportacaoFila fila,
        TimeProvider relogio,
        IOptions<EtlOpcoes> opcoes,
        ILogger<GeoImportacaoBackgroundService> logger)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(fila);
        ArgumentNullException.ThrowIfNull(relogio);
        ArgumentNullException.ThrowIfNull(opcoes);
        ArgumentNullException.ThrowIfNull(logger);

        _scopeFactory = scopeFactory;
        _fila = fila;
        _relogio = relogio;
        _limiteAbandono = opcoes.Value.LimiteAbandono;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ReconciliarOrfasAsync(stoppingToken).ConfigureAwait(false);

        await foreach (Guid execucaoId in _fila.LerAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                using IServiceScope escopo = _scopeFactory.CreateScope();
                IGeoImportacaoExecutor executor = escopo.ServiceProvider.GetRequiredService<IGeoImportacaoExecutor>();
                await executor.ExecutarAsync(execucaoId, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
#pragma warning disable CA1031 // O executor já marca a execução como falha; aqui só evitamos derrubar o loop do worker.
            catch (Exception excecao)
            {
                LogErroProcessando(_logger, execucaoId, excecao);
            }
#pragma warning restore CA1031
        }
    }

    private async Task ReconciliarOrfasAsync(CancellationToken cancellationToken)
    {
        try
        {
            using IServiceScope escopo = _scopeFactory.CreateScope();
            GeoDbContext contexto = escopo.ServiceProvider.GetRequiredService<GeoDbContext>();

            int afetadas = await GeoExecucaoReconciliador
                .ReclamarAbandonadasAsync(contexto, _relogio.GetUtcNow(), _limiteAbandono, cancellationToken)
                .ConfigureAwait(false);

            if (afetadas > 0)
            {
                LogOrfasReconciliadas(_logger, afetadas);
            }
        }
#pragma warning disable CA1031 // Reconciliação best-effort no startup: uma falha aqui não pode impedir o worker de subir.
        catch (Exception excecao) when (excecao is not OperationCanceledException)
        {
            LogErroReconciliar(_logger, excecao);
        }
#pragma warning restore CA1031
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "ETL Geo: {Afetadas} execução(ões) em andamento reconciliada(s) como falha no startup (abandonadas por reinício).")]
    private static partial void LogOrfasReconciliadas(ILogger logger, int afetadas);

    [LoggerMessage(Level = LogLevel.Error, Message = "ETL Geo: falha ao reconciliar execuções em andamento no startup.")]
    private static partial void LogErroReconciliar(ILogger logger, Exception excecao);

    [LoggerMessage(Level = LogLevel.Error, Message = "ETL Geo: erro ao processar a execução {ExecucaoId} na fila.")]
    private static partial void LogErroProcessando(ILogger logger, Guid execucaoId, Exception excecao);
}
