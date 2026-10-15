namespace Unifesspa.UniPlus.Discentes.IntegrationTests;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Unifesspa.UniPlus.Discentes.Application.Abstractions;
using Unifesspa.UniPlus.Discentes.Domain.Entities;
using Unifesspa.UniPlus.Discentes.Domain.Enums;
using Unifesspa.UniPlus.Discentes.Domain.Interfaces;
using Unifesspa.UniPlus.Discentes.Infrastructure.Persistence;
using Unifesspa.UniPlus.Discentes.Infrastructure.Persistence.Records;
using Unifesspa.UniPlus.Discentes.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Discentes.Infrastructure.Sincronizacao;

using Wolverine;

[Trait("Category", "Integration")]
public sealed class GatilhoDiarioDiscentesPersistenciaTests : IClassFixture<VinculoDiscenteDbFixture>
{
    private readonly VinculoDiscenteDbFixture _fixture;

    public GatilhoDiarioDiscentesPersistenciaTests(VinculoDiscenteDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Reentrega_do_mesmo_gatilho_nao_publica_a_sincronizacao_outra_vez()
    {
        DateOnly dia = new(2091, 4, 3);
        DateTimeOffset agora = DateTimeOffset.UtcNow;
        await using DiscentesDbContext db = _fixture.CreateDbContext();
        await InicializarGatilhoDiarioDiscentesHandler.ReservarAsync(
            db,
            dia,
            agora,
            CancellationToken.None);

        IMessageBus bus = Substitute.For<IMessageBus>();
        IOptions<GatilhoDiarioOptions> opcoes = Options.Create(new GatilhoDiarioOptions
        {
            Horario = "03:00",
            FusoHorarioId = "America/Belem",
        });
        TimeProvider relogio = TimeProvider.System;

        await GatilhoDiarioDiscentesHandler.Handle(
            new GatilhoDiarioDiscentes(dia),
            db,
            bus,
            opcoes,
            relogio,
            NullLogger<GatilhoDiarioDiscentes>.Instance,
            CancellationToken.None);
        await GatilhoDiarioDiscentesHandler.Handle(
            new GatilhoDiarioDiscentes(dia),
            db,
            bus,
            opcoes,
            relogio,
            NullLogger<GatilhoDiarioDiscentes>.Instance,
            CancellationToken.None);

        await bus.Received(1).PublishAsync(
            Arg.Is<SincronizarVinculosDiscentes>(mensagem => mensagem.DataDeReferencia == dia));
        (await db.ReservasDeSincronizacaoDiaria
            .Where(reserva => reserva.DataDeReferencia == dia)
            .Select(reserva => reserva.Estado)
            .SingleAsync())
            .Should().Be(EstadoDaSincronizacaoDiaria.Despachada);
    }

    [Theory]
    [InlineData((short)EstadoDaSincronizacaoDiaria.Concluida, SyncRunStatus.Completed, 4)]
    [InlineData((short)EstadoDaSincronizacaoDiaria.Parcial, SyncRunStatus.Partial, 5)]
    [InlineData((short)EstadoDaSincronizacaoDiaria.Falhou, SyncRunStatus.Failed, 6)]
    public async Task Reserva_terminal_nao_inicia_execucao_nem_publica_novamente(
        short estado,
        SyncRunStatus situacao,
        int diaDoMes)
    {
        DateOnly dia = new(2091, 4, diaDoMes);
        await using (DiscentesDbContext db = _fixture.CreateDbContext())
        {
            await ReservarComoAsync(db, dia, (EstadoDaSincronizacaoDiaria)estado);
            SyncRun concluida = SyncRun.Iniciar(dia, TimeProvider.System);
            concluida.Concluir(
                ContagensDaExecucao.Nenhuma,
                situacao,
                TimeProvider.System);
            db.SyncRuns.Add(concluida);
            await db.SaveChangesAsync();
        }

        using ServiceProvider provider = CriarProvider();
        RegistroDeExecucoes registro = new(
            provider.GetRequiredService<IServiceScopeFactory>(),
            TimeProvider.System);

        Guid? id = await registro.IniciarAsync(dia);

        id.Should().BeNull();
        await using DiscentesDbContext leitura = _fixture.CreateDbContext();
        (await leitura.SyncRuns.CountAsync(run => run.DataDeReferencia == dia)).Should().Be(1);

        IMessageBus bus = Substitute.For<IMessageBus>();
        await using DiscentesDbContext dbDoGatilho = _fixture.CreateDbContext();
        await GatilhoDiarioDiscentesHandler.Handle(
            new GatilhoDiarioDiscentes(dia),
            dbDoGatilho,
            bus,
            Options.Create(new GatilhoDiarioOptions
            {
                Horario = "03:00",
                FusoHorarioId = "America/Belem",
            }),
            TimeProvider.System,
            NullLogger<GatilhoDiarioDiscentes>.Instance,
            CancellationToken.None);

        await bus.DidNotReceive().PublishAsync(Arg.Any<SincronizarVinculosDiscentes>());
    }

    [Fact]
    public async Task Iniciar_retoma_o_sync_run_em_andamento_sem_criar_novo()
    {
        DateOnly dia = new(2091, 4, 7);
        Guid idExistente;
        await using (DiscentesDbContext db = _fixture.CreateDbContext())
        {
            await ReservarComoAsync(db, dia, EstadoDaSincronizacaoDiaria.Executando);
            SyncRun emAndamento = SyncRun.Iniciar(dia, TimeProvider.System);
            idExistente = emAndamento.Id;
            db.SyncRuns.Add(emAndamento);
            await db.SaveChangesAsync();
        }

        using ServiceProvider provider = CriarProvider();
        RegistroDeExecucoes registro = new(
            provider.GetRequiredService<IServiceScopeFactory>(),
            TimeProvider.System);

        Guid? retomada = await registro.IniciarAsync(dia);

        retomada.Should().Be(idExistente);
        await using DiscentesDbContext leitura = _fixture.CreateDbContext();
        (await leitura.SyncRuns.CountAsync(run => run.DataDeReferencia == dia)).Should().Be(1);
    }

    private static async Task ReservarComoAsync(
        DiscentesDbContext db,
        DateOnly dia,
        EstadoDaSincronizacaoDiaria estado)
    {
        await InicializarGatilhoDiarioDiscentesHandler.ReservarAsync(
            db,
            dia,
            DateTimeOffset.UtcNow,
            CancellationToken.None);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE discentes.sincronizacao_diaria SET estado = {(short)estado} WHERE data_de_referencia = {dia}");
    }

    private ServiceProvider CriarProvider()
    {
        ServiceCollection services = new();
        services.AddScoped(_ => _fixture.CreateDbContext());
        services.AddScoped<IDiscentesUnitOfWork>(provider =>
            provider.GetRequiredService<DiscentesDbContext>());
        services.AddScoped<ISyncRunRepository, SyncRunRepository>();
        return services.BuildServiceProvider();
    }
}
