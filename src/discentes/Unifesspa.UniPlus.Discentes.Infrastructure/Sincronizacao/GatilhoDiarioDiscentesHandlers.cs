namespace Unifesspa.UniPlus.Discentes.Infrastructure.Sincronizacao;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Discentes.Infrastructure.Persistence;
using Unifesspa.UniPlus.Discentes.Infrastructure.Persistence.Records;

using Wolverine;

public static partial class InicializarGatilhoDiarioDiscentesHandler
{
    public static async Task Handle(
        InicializarGatilhoDiarioDiscentes mensagem,
        DiscentesDbContext db,
        IMessageBus bus,
        IOptions<GatilhoDiarioOptions> opcoes,
        TimeProvider relogio,
        ILogger<InicializarGatilhoDiarioDiscentes> logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mensagem);
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(opcoes);
        ArgumentNullException.ThrowIfNull(relogio);
        ArgumentNullException.ThrowIfNull(logger);

        DateTimeOffset agora = relogio.GetUtcNow();
        ReservaDeSincronizacaoDiaria? pendente = await db.ReservasDeSincronizacaoDiaria
            .AsNoTracking()
            .Where(item => item.Estado == EstadoDaSincronizacaoDiaria.Pendente)
            .OrderBy(item => item.AgendadaEm)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (pendente is not null)
        {
            if (pendente.AgendadaEm > agora)
            {
                return;
            }

            // Reserva e agendamento original foram confirmados juntos; ao vencer o prazo,
            // reemitir a mesma data recupera uma mensagem morta sem abrir outro ciclo.
            await bus.ScheduleAsync(
                new GatilhoDiarioDiscentes(pendente.DataDeReferencia),
                agora).ConfigureAwait(false);
            LogGatilhoPendenteRecuperado(logger, pendente.DataDeReferencia, agora);
            return;
        }

        TimeOnly horario = TimeOnly.Parse(opcoes.Value.Horario, System.Globalization.CultureInfo.InvariantCulture);
        TimeZoneInfo fuso = TimeZoneInfo.FindSystemTimeZoneById(opcoes.Value.FusoHorarioId);
        DateTimeOffset proximaExecucao = CalculadoraDoProximoGatilhoDiario.Calcular(
            agora,
            horario,
            fuso);
        DateOnly dataDeReferencia = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(proximaExecucao, fuso).DateTime);

        int reservada = await ReservarAsync(
            db,
            dataDeReferencia,
            proximaExecucao,
            cancellationToken).ConfigureAwait(false);

        if (reservada == 0)
        {
            return;
        }

        await bus.ScheduleAsync(
            new GatilhoDiarioDiscentes(dataDeReferencia),
            proximaExecucao).ConfigureAwait(false);

        LogGatilhoInicialAgendado(logger, dataDeReferencia, proximaExecucao);
    }

    internal static Task<int> ReservarAsync(
        DiscentesDbContext db,
        DateOnly dataDeReferencia,
        DateTimeOffset agendadaEm,
        CancellationToken cancellationToken)
    {
        DateTimeOffset agendadaEmUtc = agendadaEm.ToUniversalTime();

        return db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO discentes.sincronizacao_diaria
                (data_de_referencia, estado, agendada_em)
            VALUES
                ({dataDeReferencia}, {(short)EstadoDaSincronizacaoDiaria.Pendente}, {agendadaEmUtc})
            ON CONFLICT (data_de_referencia) DO NOTHING
            """,
            cancellationToken);
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Primeiro gatilho diário de discentes agendado. Data={DataDeReferencia} Instante={Instante}")]
    private static partial void LogGatilhoInicialAgendado(
        ILogger logger,
        DateOnly dataDeReferencia,
        DateTimeOffset instante);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Gatilho diário pendente recuperado no startup. Data={DataDeReferencia} Instante={Instante}")]
    private static partial void LogGatilhoPendenteRecuperado(
        ILogger logger,
        DateOnly dataDeReferencia,
        DateTimeOffset instante);
}

public static partial class GatilhoDiarioDiscentesHandler
{
    public static async Task Handle(
        GatilhoDiarioDiscentes mensagem,
        DiscentesDbContext db,
        IMessageBus bus,
        IOptions<GatilhoDiarioOptions> opcoes,
        TimeProvider relogio,
        ILogger<GatilhoDiarioDiscentes> logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mensagem);
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(opcoes);
        ArgumentNullException.ThrowIfNull(relogio);
        ArgumentNullException.ThrowIfNull(logger);

        int despachada = await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE discentes.sincronizacao_diaria
            SET estado = {(short)EstadoDaSincronizacaoDiaria.Despachada},
                despachada_em = {relogio.GetUtcNow()}
            WHERE data_de_referencia = {mensagem.DataDeReferencia}
              AND estado = {(short)EstadoDaSincronizacaoDiaria.Pendente}
            """,
            cancellationToken).ConfigureAwait(false);

        if (despachada == 0)
        {
            bool existe = await db.ReservasDeSincronizacaoDiaria
                .AsNoTracking()
                .AnyAsync(item => item.DataDeReferencia == mensagem.DataDeReferencia, cancellationToken)
                .ConfigureAwait(false);

            if (!existe)
            {
                throw new InvalidOperationException(
                    $"O gatilho de {mensagem.DataDeReferencia:yyyy-MM-dd} não tem reserva persistida.");
            }

            LogGatilhoDuplicado(logger, mensagem.DataDeReferencia);
            return;
        }

        TimeOnly horario = TimeOnly.Parse(opcoes.Value.Horario, System.Globalization.CultureInfo.InvariantCulture);
        TimeZoneInfo fuso = TimeZoneInfo.FindSystemTimeZoneById(opcoes.Value.FusoHorarioId);
        DateTimeOffset proximaExecucao = CalculadoraDoProximoGatilhoDiario.Calcular(
            relogio.GetUtcNow(),
            horario,
            fuso,
            mensagem.DataDeReferencia);
        DateOnly proximaData = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(proximaExecucao, fuso).DateTime);

        int proximaReservada = await InicializarGatilhoDiarioDiscentesHandler.ReservarAsync(
            db,
            proximaData,
            proximaExecucao,
            cancellationToken).ConfigureAwait(false);

        if (proximaReservada == 1)
        {
            await bus.ScheduleAsync(
                new GatilhoDiarioDiscentes(proximaData),
                proximaExecucao).ConfigureAwait(false);
        }

        await bus.PublishAsync(new SincronizarVinculosDiscentes(mensagem.DataDeReferencia))
            .ConfigureAwait(false);

        LogGatilhoDespachado(logger, mensagem.DataDeReferencia, proximaData);
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Gatilho diário duplicado ignorado. Data={DataDeReferencia}")]
    private static partial void LogGatilhoDuplicado(ILogger logger, DateOnly dataDeReferencia);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Gatilho diário despachou a sincronização e agendou o próximo ciclo. "
            + "Data={DataDeReferencia} ProximaData={ProximaData}")]
    private static partial void LogGatilhoDespachado(
        ILogger logger,
        DateOnly dataDeReferencia,
        DateOnly proximaData);
}
