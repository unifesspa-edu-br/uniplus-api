namespace Unifesspa.UniPlus.Verificador.Antivirus;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Unifesspa.UniPlus.Verificador.Observabilidade;

/// <summary>Lê de tempos em tempos a data da base de assinaturas em uso, que alimenta o alerta de três dias.</summary>
public sealed partial class IdadeDaBaseWorker(IClamd clamd, MetricasDoVerificador metricas, ILogger<IdadeDaBaseWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer relogio = new(Intervalo);
        do
        {
            try
            {
                metricas.AtualizarDataDaBase(await clamd.LerDataDaBaseAsync(stoppingToken).ConfigureAwait(false));
            }
            catch (ClamdException ex)
            {
                LogFalhaNaLeitura(logger, ex);
            }
        }
        while (await relogio.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Não foi possível ler a data da base do clamd")]
    private static partial void LogFalhaNaLeitura(ILogger logger, Exception exception);
}
