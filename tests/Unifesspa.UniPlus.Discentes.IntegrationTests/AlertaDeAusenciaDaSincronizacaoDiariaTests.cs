namespace Unifesspa.UniPlus.Discentes.IntegrationTests;

using System.Collections.Concurrent;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;

using NSubstitute;

using Unifesspa.UniPlus.Discentes.API;
using Unifesspa.UniPlus.Discentes.Infrastructure.Persistence;
using Unifesspa.UniPlus.Discentes.Infrastructure.Sincronizacao;

using Wolverine;

[Trait("Category", "Integration")]
public sealed class AlertaDeAusenciaDaSincronizacaoDiariaTests : IClassFixture<VinculoDiscenteDbFixture>
{
    // 04:00 em America/Belem (UTC-3): já passou do limite de 03:00, então a verificação roda de imediato.
    private static readonly DateTimeOffset Agora = new(2092, 5, 4, 7, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Dia = new(2092, 5, 4);

    private readonly VinculoDiscenteDbFixture _fixture;

    public AlertaDeAusenciaDaSincronizacaoDiariaTests(VinculoDiscenteDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Dia_sem_execucao_apos_o_limite_grava_o_alerta_uma_unica_vez()
    {
        await using DiscentesDbContext banco = _fixture.CreateDbContext();
        await banco.Database.ExecuteSqlRawAsync("TRUNCATE discentes.alerta_sincronizacao_diaria");
        IMessageBus bus = Substitute.For<IMessageBus>();
        using ServiceProvider provedor = Provedor(() => _fixture.CreateDbContext(), bus);
        ListaDeLogs logs = new();

        using AlertaDeAusenciaDaSincronizacaoDiaria servico = Criar(provedor, logs);
        await servico.StartAsync(CancellationToken.None);
        await AguardarAsync(async () => await ContarAlertasAsync(banco) == 1);
        await servico.StopAsync(CancellationToken.None);

        (await ContarAlertasAsync(banco)).Should().Be(1);
        logs.Niveis.Should().Contain(LogLevel.Error);
        await bus.Received(1).InvokeAsync(
            Arg.Any<InicializarGatilhoDiarioDiscentes>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<TimeSpan?>());
    }

    [Fact]
    public async Task Banco_indisponivel_nao_derruba_o_servico()
    {
        NpgsqlConnectionStringBuilder semBanco = new(_fixture.ConnectionString) { Port = 1, Timeout = 1 };
        using ServiceProvider provedor = Provedor(() => new DiscentesDbContext(
            new DbContextOptionsBuilder<DiscentesDbContext>()
                .UseNpgsql(semBanco.ConnectionString)
                .UseSnakeCaseNamingConvention()
                .Options), Substitute.For<IMessageBus>());
        ListaDeLogs logs = new();

        using AlertaDeAusenciaDaSincronizacaoDiaria servico = Criar(provedor, logs);
        await servico.StartAsync(CancellationToken.None);
        await AguardarAsync(() => Task.FromResult(logs.Niveis.Contains(LogLevel.Error)));

        servico.ExecuteTask!.IsCompleted.Should().BeFalse("a falha do banco é registrada, não propagada ao host");
        await servico.StopAsync(CancellationToken.None);
    }

    private static AlertaDeAusenciaDaSincronizacaoDiaria Criar(ServiceProvider provedor, ListaDeLogs logs) =>
        new(
            provedor.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new GatilhoDiarioOptions
            {
                HorarioLimiteAlerta = "03:00",
                FusoHorarioAlertaId = "America/Belem",
                CanalAlerta = "discentes-sync",
            }),
            new RelogioFixo(Agora),
            logs);

    private static ServiceProvider Provedor(Func<DiscentesDbContext> criarBanco, IMessageBus bus)
    {
        ServiceCollection servicos = new();
        servicos.AddSingleton(bus);
        servicos.AddScoped(_ => criarBanco());
        return servicos.BuildServiceProvider();
    }

    private static Task<int> ContarAlertasAsync(DiscentesDbContext banco) =>
        banco.Database
            .SqlQuery<int>(
                $"SELECT count(*)::int AS \"Value\" FROM discentes.alerta_sincronizacao_diaria WHERE data_de_referencia = {Dia}")
            .SingleAsync();

    private static async Task AguardarAsync(Func<Task<bool>> condicao)
    {
        DateTime limite = DateTime.UtcNow.AddSeconds(15);
        while (!await condicao())
        {
            if (DateTime.UtcNow > limite)
            {
                throw new TimeoutException("A condição esperada não ocorreu a tempo.");
            }

            await Task.Delay(50);
        }
    }

    private sealed class RelogioFixo(DateTimeOffset agora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => agora;
    }

    private sealed class ListaDeLogs : ILogger<AlertaDeAusenciaDaSincronizacaoDiaria>
    {
        public ConcurrentBag<LogLevel> Niveis { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) => Niveis.Add(logLevel);
    }
}
