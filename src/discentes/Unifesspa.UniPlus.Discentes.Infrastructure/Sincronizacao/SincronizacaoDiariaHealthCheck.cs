namespace Unifesspa.UniPlus.Discentes.Infrastructure.Sincronizacao;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using Unifesspa.UniPlus.Discentes.Infrastructure.Persistence;
using Unifesspa.UniPlus.Discentes.Infrastructure.Persistence.Records;

/// <summary>
/// Informa se a sincronização diária de discentes está correndo normalmente.
/// </summary>
/// <remarks>
/// Degradado, nunca indisponível: a carga é auxiliar e tolera perder uma janela — a próxima
/// tenta de novo. O status só chama a atenção de quem opera; o pod continua no Service.
/// </remarks>
public sealed class SincronizacaoDiariaHealthCheck(DiscentesDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        ReservaDeSincronizacaoDiaria? ultima = await db.ReservasDeSincronizacaoDiaria
            .AsNoTracking()
            .Where(item => item.Estado != EstadoDaSincronizacaoDiaria.Pendente)
            .OrderByDescending(item => item.DataDeReferencia)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        DateOnly? ultimoAlerta = await db.AlertasDeSincronizacaoDiaria
            .AsNoTracking()
            .MaxAsync(item => (DateOnly?)item.DataDeReferencia, cancellationToken)
            .ConfigureAwait(false);

        if (ultimoAlerta is { } alerta && (ultima is null || alerta > ultima.DataDeReferencia))
        {
            return HealthCheckResult.Degraded($"A sincronização de {alerta:yyyy-MM-dd} não iniciou até o limite.");
        }

        if (ultima is { Estado: EstadoDaSincronizacaoDiaria.Parcial or EstadoDaSincronizacaoDiaria.Falhou })
        {
            return HealthCheckResult.Degraded(
                $"A sincronização de {ultima.DataDeReferencia:yyyy-MM-dd} terminou como {ultima.Estado}.");
        }

        return HealthCheckResult.Healthy();
    }
}
