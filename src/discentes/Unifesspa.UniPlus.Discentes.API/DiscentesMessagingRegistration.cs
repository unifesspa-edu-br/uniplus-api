namespace Unifesspa.UniPlus.Discentes.API;

using JasperFx;
using JasperFx.CodeGeneration;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Discentes.Infrastructure.Persistence;
using Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa;
using Unifesspa.UniPlus.Discentes.Infrastructure.Sincronizacao;

using Wolverine;
using Wolverine.Configuration;
using Wolverine.ErrorHandling;
using Wolverine.Runtime.Handlers;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "Chamado pelo composition root do host após configurar o runtime Wolverine.")]
public static class DiscentesMessagingRegistration
{
    public static IServiceCollection AddGatilhoDiarioDiscentes(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<GatilhoDiarioOptions>()
            .Bind(configuration.GetSection(GatilhoDiarioOptions.SectionName));
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<GatilhoDiarioOptions>, GatilhoDiarioOptionsValidator>());

        if (!SigaaConfigurado(configuration))
        {
            return services;
        }

        services.AddOptions<GatilhoDiarioOptions>().ValidateOnStart();
        services.AddHostedService<InicializadorDoGatilhoDiarioDiscentes>();
        services.AddHostedService<AlertaDeAusenciaDaSincronizacaoDiaria>();

        // Fora da tag `ready`: a carga é auxiliar, e degradada o /health responde 200 sem tirar o pod do Service.
        services.AddHealthChecks().AddCheck<SincronizacaoDiariaHealthCheck>(
            name: "sincronizacao-discentes",
            failureStatus: HealthStatus.Degraded,
            tags: ["discentes"],
            timeout: TimeSpan.FromSeconds(2));

        return services;
    }

    public static void ConfigurarRouting(WolverineOptions opts, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(opts);
        ArgumentNullException.ThrowIfNull(configuration);

        bool sigaaConfigurado = SigaaConfigurado(configuration);
        GatilhoDiarioOptions opcoes = configuration
            .GetSection(GatilhoDiarioOptions.SectionName)
            .Get<GatilhoDiarioOptions>() ?? new GatilhoDiarioOptions();
        TimeSpan[] cooldowns;

        if (sigaaConfigurado)
        {
            ValidateOptionsResult validacao = new GatilhoDiarioOptionsValidator().Validate(name: null, opcoes);
            if (validacao.Failed)
            {
                throw new InvalidOperationException(
                    $"Configuração do gatilho diário inválida: {string.Join(" | ", validacao.Failures ?? [])}");
            }

            opts.Durability.StaleNodeTimeout = TimeSpan.FromSeconds(opcoes.PrazoReatribuicaoSegundos);
            cooldowns = opcoes.RetryAposSegundos.Select(segundos => TimeSpan.FromSeconds(segundos)).ToArray();
        }
        else
        {
            // O host de migration e os ambientes sem SIGAA não ativam o gatilho.
            // Mantém-se a política atual apenas para que o código gerado seja igual.
            cooldowns = [TimeSpan.FromMilliseconds(200), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5)];
        }

        opts.LocalQueueFor<GatilhoDiarioDiscentes>().UseDurableInbox();
        opts.LocalQueueFor<SincronizarVinculosDiscentes>().UseDurableInbox();
        opts.Policies.Add(new RetryDoGatilhoDiarioDiscentes(cooldowns));
        opts.Policies.Add<FalhaDaSincronizacaoVaiParaDeadLetter>();
    }

    private static bool SigaaConfigurado(IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(configuration[$"{SigaaOptions.SectionName}:{nameof(SigaaOptions.BaseUrl)}"]);
}

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "Registrado como IHostedService pelo composition root do host.")]
public sealed partial class InicializadorDoGatilhoDiarioDiscentes(
    IServiceScopeFactory escopos,
    ILogger<InicializadorDoGatilhoDiarioDiscentes> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using AsyncServiceScope escopo = escopos.CreateAsyncScope();
            IMessageBus bus = escopo.ServiceProvider.GetRequiredService<IMessageBus>();
            await bus.InvokeAsync(new InicializarGatilhoDiarioDiscentes(), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception excecao) when (excecao is not OperationCanceledException)
        {
            // A sincronização é auxiliar: falhar ao agendar não pode impedir a subida dos demais módulos.
            LogFalhaAoInicializar(logger, excecao);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Falha ao inicializar o gatilho diário de discentes; o agendamento ocorrerá na próxima subida.")]
    private static partial void LogFalhaAoInicializar(ILogger logger, Exception excecao);
}

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "Registrado como IHostedService pelo composition root do host.")]
public sealed partial class AlertaDeAusenciaDaSincronizacaoDiaria : BackgroundService
{
    private readonly IServiceScopeFactory _escopos;
    private readonly IOptions<GatilhoDiarioOptions> _opcoes;
    private readonly TimeProvider _relogio;
    private readonly ILogger<AlertaDeAusenciaDaSincronizacaoDiaria> _logger;

    public AlertaDeAusenciaDaSincronizacaoDiaria(
        IServiceScopeFactory escopos,
        IOptions<GatilhoDiarioOptions> opcoes,
        TimeProvider relogio,
        ILogger<AlertaDeAusenciaDaSincronizacaoDiaria> logger)
    {
        ArgumentNullException.ThrowIfNull(escopos);
        ArgumentNullException.ThrowIfNull(opcoes);
        ArgumentNullException.ThrowIfNull(relogio);
        ArgumentNullException.ThrowIfNull(logger);

        _escopos = escopos;
        _opcoes = opcoes;
        _relogio = relogio;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        TimeOnly horarioLimite = TimeOnly.Parse(
            _opcoes.Value.HorarioLimiteAlerta,
            System.Globalization.CultureInfo.InvariantCulture);
        TimeZoneInfo fuso = TimeZoneInfo.FindSystemTimeZoneById(_opcoes.Value.FusoHorarioAlertaId);
        bool verificarDiaAtual = true;

        while (!stoppingToken.IsCancellationRequested)
        {
            DateTimeOffset agora = _relogio.GetUtcNow();
            DateTimeOffset local = TimeZoneInfo.ConvertTime(agora, fuso);
            DateOnly dataLocal = DateOnly.FromDateTime(local.DateTime);
            DateTimeOffset limite;

            if (verificarDiaAtual && TimeOnly.FromDateTime(local.DateTime) >= horarioLimite)
            {
                limite = agora;
            }
            else
            {
                limite = CalculadoraDoProximoGatilhoDiario.Calcular(
                    agora,
                    horarioLimite,
                    fuso,
                    verificarDiaAtual ? null : dataLocal);
                dataLocal = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(limite, fuso).DateTime);
            }

            TimeSpan espera = limite - agora;
            if (espera > TimeSpan.Zero)
            {
                await Task.Delay(espera, _relogio, stoppingToken).ConfigureAwait(false);
            }

            try
            {
                await VerificarDiaAsync(dataLocal, stoppingToken).ConfigureAwait(false);
            }
            catch (Exception excecao) when (excecao is not OperationCanceledException)
            {
                // Uma falha transitória do banco não pode derrubar o host: exceção não tratada
                // em BackgroundService encerra o processo, e com ele todos os módulos.
                LogFalhaAoVerificarDia(_logger, dataLocal, excecao);
            }

            verificarDiaAtual = false;
        }
    }

    private async Task VerificarDiaAsync(DateOnly dataDeReferencia, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope escopo = _escopos.CreateAsyncScope();
        DiscentesDbContext db = escopo.ServiceProvider.GetRequiredService<DiscentesDbContext>();
        DateTimeOffset emitidaEm = _relogio.GetUtcNow();

        int emitido = await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO discentes.alerta_sincronizacao_diaria (data_de_referencia, emitida_em)
            SELECT {dataDeReferencia}, {emitidaEm}
            WHERE NOT EXISTS (
                SELECT 1
                FROM discentes.sync_run
                WHERE data_de_referencia = {dataDeReferencia})
            ON CONFLICT (data_de_referencia) DO NOTHING
            """,
            cancellationToken).ConfigureAwait(false);

        if (emitido == 1)
        {
            LogDiaSemExecucao(
                _logger,
                dataDeReferencia,
                _opcoes.Value.CanalAlerta,
                _opcoes.Value.HorarioLimiteAlerta,
                _opcoes.Value.FusoHorarioAlertaId);
        }
    }

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Falha ao verificar a sincronização diária de discentes. Data={DataDeReferencia}")]
    private static partial void LogFalhaAoVerificarDia(
        ILogger logger,
        DateOnly dataDeReferencia,
        Exception excecao);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "ALERTA: sincronização diária de discentes não iniciou até o limite. "
            + "Data={DataDeReferencia} Canal={CanalAlerta} HorarioLimite={HorarioLimite} Fuso={FusoHorario}")]
    private static partial void LogDiaSemExecucao(
        ILogger logger,
        DateOnly dataDeReferencia,
        string canalAlerta,
        string horarioLimite,
        string fusoHorario);
}

internal sealed class RetryDoGatilhoDiarioDiscentes(TimeSpan[] cooldowns) : IHandlerPolicy
{
    private readonly TimeSpan[] _cooldowns = cooldowns;

    public void Apply(IReadOnlyList<HandlerChain> chains, GenerationRules rules, IServiceContainer container)
    {
        ArgumentNullException.ThrowIfNull(chains);

        foreach (HandlerChain chain in chains.Where(item => item.MessageType == typeof(GatilhoDiarioDiscentes)))
        {
            // A política é configurada pelo host a partir dos valores operacionais aprovados.
            chain.OnException<Exception>()
                .RetryWithCooldown(_cooldowns)
                .Then.MoveToErrorQueue();
        }
    }
}

internal sealed class FalhaDaSincronizacaoVaiParaDeadLetter : IHandlerPolicy
{
    public void Apply(IReadOnlyList<HandlerChain> chains, GenerationRules rules, IServiceContainer container)
    {
        ArgumentNullException.ThrowIfNull(chains);

        foreach (HandlerChain chain in chains.Where(item => item.MessageType == typeof(SincronizarVinculosDiscentes)))
        {
            chain.OnException<Exception>().MoveToErrorQueue();
        }
    }
}
