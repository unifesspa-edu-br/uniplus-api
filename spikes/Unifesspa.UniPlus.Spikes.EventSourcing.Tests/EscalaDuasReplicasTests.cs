using AwesomeAssertions;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;
using Unifesspa.UniPlus.Spikes.EventSourcing.Application.Comandos;
using Unifesspa.UniPlus.Spikes.EventSourcing.Domain;
using Unifesspa.UniPlus.Spikes.EventSourcing.Infrastructure;
using Wolverine;
using Xunit;

namespace Unifesspa.UniPlus.Spikes.EventSourcing.Tests;

/// <summary>
/// Escala horizontal — caminho ponta a ponta. Duas réplicas da MESMA API (dois
/// <see cref="IHost"/> em <c>DurabilityMode.Balanced</c> sobre um Postgres) recebem
/// comandos <c>RetificarEdital</c> concorrentes para o MESMO stream. O handler
/// <c>[WriteAggregate]</c> processa via <c>FetchForWriting</c>; o Wolverine retenta
/// os conflitos de concorrência (política em <see cref="ConfiguracaoSpike"/>); o
/// estado final reflete TODAS as retificações — sem lost update.
/// </summary>
public sealed class EscalaDuasReplicasTests : IAsyncLifetime
{
    private const int Retificacoes = 6;

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("uniplus_spike_escala")
        .WithUsername("uniplus_spike")
        .WithPassword("uniplus_spike")
        .Build();

    private IHost _replicaA = null!;
    private IHost _replicaB = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        string conn = _postgres.GetConnectionString();
        _replicaA = ConfiguracaoSpike.CriarHost(conn, DurabilityMode.Balanced).Build();
        await _replicaA.StartAsync();
        _replicaB = ConfiguracaoSpike.CriarHost(conn, DurabilityMode.Balanced).Build();
        await _replicaB.StartAsync();
    }

    public async Task DisposeAsync()
    {
        foreach (IHost r in new[] { _replicaA, _replicaB }.OfType<IHost>())
        {
            await r.StopAsync();
            r.Dispose();
        }

        await _postgres.DisposeAsync();
    }

    [Fact(DisplayName = "Escala ponta a ponta: 2 réplicas Balanced retificam o mesmo stream sem lost update")]
    public async Task Duas_replicas_retificam_mesmo_stream_sem_lost_update()
    {
        IMessageBus busA = _replicaA.Services.GetRequiredService<IMessageBus>();
        IMessageBus busB = _replicaB.Services.GetRequiredService<IMessageBus>();

        // Arrange: edital aberto + publicado (via réplica A).
        Guid editalId = Guid.CreateVersion7();
        await busA.InvokeAsync(new AbrirEdital(editalId, "050/2026", "Edital escala", TestHelpers.AtorFicticio()));
        await busA.InvokeAsync(new PublicarEdital(editalId, "hash-config-v1", TestHelpers.AtorFicticio()));

        // Act: N retificações concorrentes para o MESMO stream, divididas entre as duas
        // réplicas (cada uma processa as suas via outbox durável local).
        List<Task> envios = [];
        for (int i = 0; i < Retificacoes; i++)
        {
            IMessageBus bus = i % 2 == 0 ? busA : busB;
            envios.Add(bus.PublishAsync(
                new RetificarEdital(editalId, $"retificação {i}", TestHelpers.AtorFicticio())).AsTask());
        }

        await Task.WhenAll(envios);

        // Assert: todas as N retificações convergem no agregado (Wolverine retentou os
        // conflitos de concorrência), sem nenhuma perdida.
        bool convergiu = await TestHelpers.EsperarAsync(
            () => RetificacoesAplicadasAsync(editalId).GetAwaiter().GetResult() == Retificacoes,
            TimeSpan.FromSeconds(45));

        int total = await RetificacoesAplicadasAsync(editalId);
        convergiu.Should().BeTrue(
            $"as {Retificacoes} retificações devem convergir sem lost update (aplicadas: {total})");
    }

    private async Task<int> RetificacoesAplicadasAsync(Guid editalId)
    {
        IDocumentStore store = _replicaA.Services.GetRequiredService<IDocumentStore>();
        await using IQuerySession session = store.QuerySession();
        EditalEs? view = await session.LoadAsync<EditalEs>(editalId);
        return view?.QuantidadeRetificacoes ?? 0;
    }
}
