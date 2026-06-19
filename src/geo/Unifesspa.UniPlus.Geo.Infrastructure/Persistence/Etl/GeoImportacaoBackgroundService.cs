namespace Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Etl;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Consome a fila de disparos do ETL (Story #674) e executa cada carga num escopo de DI
/// próprio (o orquestrador e o <c>GeoDbContext</c> são scoped). A reconciliação de execuções
/// <c>EmAndamento</c> abandonadas por crash/restart roda <strong>antes</strong> deste worker,
/// no <see cref="GeoReconciliacaoStartupHostedService"/> (#695), cujo <c>StartAsync</c> é
/// aguardado — então quando o worker começa a consumir a fila o índice único parcial já foi
/// liberado das órfãs.
/// </summary>
internal sealed partial class GeoImportacaoBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IGeoImportacaoFila _fila;
    private readonly ILogger<GeoImportacaoBackgroundService> _logger;

    public GeoImportacaoBackgroundService(
        IServiceScopeFactory scopeFactory,
        IGeoImportacaoFila fila,
        ILogger<GeoImportacaoBackgroundService> logger)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(fila);
        ArgumentNullException.ThrowIfNull(logger);

        _scopeFactory = scopeFactory;
        _fila = fila;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
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

        await FalharPendentesAsync(pendentes, cancellationToken).ConfigureAwait(false);
    }

    private async Task FalharPendentesAsync(IReadOnlyList<Guid> pendentes, CancellationToken cancellationToken)
    {
        try
        {
            using IServiceScope escopo = _scopeFactory.CreateScope();
            IGeoImportacaoExecutor executor = escopo.ServiceProvider.GetRequiredService<IGeoImportacaoExecutor>();

            foreach (Guid execucaoId in pendentes)
            {
                // Honra o token de desligamento do host: se o timeout de shutdown estourar
                // (ex.: banco lento), o dreno cede em vez de prender o processo. A
                // reconciliação por idade reclama qualquer execução não marcada (rede de
                // segurança), então ceder aqui não deixa a linha presa além do limite.
                cancellationToken.ThrowIfCancellationRequested();
                await executor.MarcarInterrompidaNoDesligamentoAsync(execucaoId, cancellationToken).ConfigureAwait(false);
                LogInterrompidaNoDesligamento(_logger, execucaoId);
            }
        }
        // Cancelamento (timeout de shutdown) é esperado — não é falha de dreno; propaga
        // silenciosamente. Outras exceções são best-effort: logam sem impedir o host de parar.
#pragma warning disable CA1031 // Dreno best-effort no desligamento; a reconciliação por idade ainda reclama a linha.
        catch (Exception excecao) when (excecao is not OperationCanceledException)
        {
            LogErroDrenarFila(_logger, excecao);
        }
#pragma warning restore CA1031
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "ETL Geo: erro ao processar a execução {ExecucaoId} na fila.")]
    private static partial void LogErroProcessando(ILogger logger, Guid execucaoId, Exception excecao);

    [LoggerMessage(Level = LogLevel.Warning, Message = "ETL Geo: execução {ExecucaoId} enfileirada e não processada foi marcada como falha no desligamento do worker.")]
    private static partial void LogInterrompidaNoDesligamento(ILogger logger, Guid execucaoId);

    [LoggerMessage(Level = LogLevel.Error, Message = "ETL Geo: falha ao drenar a fila no desligamento do worker.")]
    private static partial void LogErroDrenarFila(ILogger logger, Exception excecao);
}
