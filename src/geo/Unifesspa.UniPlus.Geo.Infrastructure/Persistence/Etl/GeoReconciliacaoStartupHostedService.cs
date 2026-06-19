namespace Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Etl;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Reconcilia execuções <c>EmAndamento</c> abandonadas por crash/restart (Story #674) ANTES
/// de o host aceitar disparos (#695). Diferente de um <see cref="BackgroundService"/> — cujo
/// <c>StartAsync</c> retorna no primeiro <c>await</c> do <c>ExecuteAsync</c>, deixando a
/// reconciliação correr concorrente ao seed e ao servidor HTTP — este é um
/// <see cref="IHostedService"/> simples cujo <see cref="StartAsync"/> é <strong>aguardado</strong>
/// pelo host. Com <c>HostOptions.ServicesStartConcurrently = false</c> (default), a ordem de
/// start é a de registro: registrado antes do worker e do seed, garante que a reconciliação
/// conclua antes de qualquer disparo — fechando a janela em que um seed/disparo imediato
/// colidiria no índice único parcial com uma órfã ainda não marcada <c>Falhou</c>.
/// </summary>
internal sealed partial class GeoReconciliacaoStartupHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _relogio;
    private readonly TimeSpan _limiteAbandono;
    private readonly ILogger<GeoReconciliacaoStartupHostedService> _logger;

    public GeoReconciliacaoStartupHostedService(
        IServiceScopeFactory scopeFactory,
        TimeProvider relogio,
        IOptions<EtlOpcoes> opcoes,
        ILogger<GeoReconciliacaoStartupHostedService> logger)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(relogio);
        ArgumentNullException.ThrowIfNull(opcoes);
        ArgumentNullException.ThrowIfNull(logger);

        _scopeFactory = scopeFactory;
        _relogio = relogio;
        _limiteAbandono = opcoes.Value.LimiteAbandono;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
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
#pragma warning disable CA1031 // Reconciliação best-effort no startup: uma falha aqui (ex.: banco transitório) não pode impedir o host de subir.
        catch (Exception excecao) when (excecao is not OperationCanceledException)
        {
            LogErroReconciliar(_logger, excecao);
        }
#pragma warning restore CA1031
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Warning, Message = "ETL Geo: {Afetadas} execução(ões) em andamento reconciliada(s) como falha no startup (abandonadas por reinício).")]
    private static partial void LogOrfasReconciliadas(ILogger logger, int afetadas);

    [LoggerMessage(Level = LogLevel.Error, Message = "ETL Geo: falha ao reconciliar execuções em andamento no startup.")]
    private static partial void LogErroReconciliar(ILogger logger, Exception excecao);
}
