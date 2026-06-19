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

    /// <summary>
    /// No desligamento, drena a fila e marca <c>Falhou</c> as execuções enfileiradas que
    /// não chegaram a ser processadas (#694). O <c>stoppingToken</c> é cancelado antes de o
    /// writer fechar, então um disparo enfileirado na janela imediatamente anterior ao
    /// shutdown pode não ser entregue pelo <c>ReadAllAsync(stoppingToken)</c>; sem este
    /// dreno, a linha ficaria <c>EmAndamento</c> bloqueando novos disparos (409) até a
    /// reconciliação por idade (<c>LimiteAbandono</c>, default 6h).
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        // Encerra o loop do ExecuteAsync (cancela o stoppingToken e aguarda). Uma execução
        // em voo é marcada Falhou pelo próprio ExecutarAsync (catch de cancelamento).
        await base.StopAsync(cancellationToken).ConfigureAwait(false);

        // Fecha o writer (sem novos enfileiramentos — IniciarAsync trata ChannelClosedException)
        // e drena o que sobrou de forma não-bloqueante.
        _fila.Completar();
        IReadOnlyList<Guid> pendentes = _fila.DrenarRestante();
        if (pendentes.Count == 0)
        {
            return;
        }

        await FalharPendentesAsync(pendentes).ConfigureAwait(false);
    }

    private async Task FalharPendentesAsync(IReadOnlyList<Guid> pendentes)
    {
        try
        {
            using IServiceScope escopo = _scopeFactory.CreateScope();
            IGeoImportacaoExecutor executor = escopo.ServiceProvider.GetRequiredService<IGeoImportacaoExecutor>();

            foreach (Guid execucaoId in pendentes)
            {
                // CancellationToken.None: o token de shutdown já está cancelado; este dreno
                // precisa concluir para deixar as linhas num estado terminal consistente.
                await executor.MarcarInterrompidaNoDesligamentoAsync(execucaoId, CancellationToken.None).ConfigureAwait(false);
                LogInterrompidaNoDesligamento(_logger, execucaoId);
            }
        }
#pragma warning disable CA1031 // Dreno best-effort no desligamento: uma falha aqui não pode impedir o host de parar; a reconciliação por idade ainda reclama a linha.
        catch (Exception excecao)
        {
            LogErroDrenarFila(_logger, excecao);
        }
#pragma warning restore CA1031
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "ETL Geo: execução {ExecucaoId} enfileirada e não processada foi marcada como falha no desligamento do worker.")]
    private static partial void LogInterrompidaNoDesligamento(ILogger logger, Guid execucaoId);

    [LoggerMessage(Level = LogLevel.Error, Message = "ETL Geo: falha ao drenar a fila no desligamento do worker.")]
    private static partial void LogErroDrenarFila(ILogger logger, Exception excecao);
}
