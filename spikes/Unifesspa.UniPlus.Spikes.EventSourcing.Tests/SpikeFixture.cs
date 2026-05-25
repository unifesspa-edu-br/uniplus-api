using Marten;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;
using Unifesspa.UniPlus.Spikes.EventSourcing.Infrastructure;
using Wolverine;
using Xunit;

namespace Unifesspa.UniPlus.Spikes.EventSourcing.Tests;

/// <summary>
/// Sobe um PostgreSQL 18 efêmero (Testcontainers) e o host do spike (Marten +
/// Wolverine) uma vez por coleção de testes. Os ids de stream são Guid v7 únicos
/// por teste, então não há colisão entre cenários — sem necessidade de limpar.
/// </summary>
public sealed class SpikeFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("uniplus_spike_es")
        .WithUsername("uniplus_spike")
        .WithPassword("uniplus_spike")
        .Build();

    private IHost? _host;

    public IHost Host => _host ?? throw new InvalidOperationException("Host não inicializado.");

    public string ConnectionString => _postgres.GetConnectionString();

    /// <summary>Barramento Wolverine para invocar comandos.</summary>
    public IMessageBus Bus => Host.Services.GetRequiredService<IMessageBus>();

    /// <summary>Event Store / document store do Marten para leituras nos asserts.</summary>
    public IDocumentStore Store => Host.Services.GetRequiredService<IDocumentStore>();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        _host = ConfiguracaoSpike.CriarHost(ConnectionString).Build();
        await _host.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        await _postgres.DisposeAsync();
    }
}

/// <summary>Coleção que compartilha um único <see cref="SpikeFixture"/>.</summary>
[CollectionDefinition(Nome)]
public sealed class ColecaoSpike : ICollectionFixture<SpikeFixture>
{
    public const string Nome = "Spike Event Sourcing";
}
