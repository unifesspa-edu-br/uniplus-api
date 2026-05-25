using Marten;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;
using Unifesspa.UniPlus.Spikes.EventSourcing.Coexistencia;
using Wolverine;
using Xunit;

namespace Unifesspa.UniPlus.Spikes.EventSourcing.Tests;

/// <summary>
/// Sobe um PostgreSQL 18 efêmero e o host de coabitação (Marten + EF Core num só
/// processo). Se este <c>InitializeAsync</c> falhar, a configuração dual-store não
/// compõe — esse é o sinal de fail-fast do gate G2 host-level.
/// </summary>
public sealed class CoexistenciaFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("uniplus_spike_cx")
        .WithUsername("uniplus_spike")
        .WithPassword("uniplus_spike")
        .Build();

    private IHost? _host;

    public IHost Host => _host ?? throw new InvalidOperationException("Host não inicializado.");

    public string ConnectionString => _postgres.GetConnectionString();

    public IMessageBus Bus => Host.Services.GetRequiredService<IMessageBus>();

    public IEditalEsStore StoreEs => Host.Services.GetRequiredService<IEditalEsStore>();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        _host = ConfiguracaoCoexistencia.CriarHost(ConnectionString).Build();
        await _host.StartAsync();

        // Cria as tabelas do módulo CRUD (o banco já existe por causa do Marten/Wolverine,
        // então EnsureCreated seria no-op; CreateTablesAsync cria só o schema do DbContext).
        using IServiceScope escopo = _host.Services.CreateScope();
        CrudDbContext db = escopo.ServiceProvider.GetRequiredService<CrudDbContext>();
        IRelationalDatabaseCreator criador = db.GetService<IRelationalDatabaseCreator>();
        await criador.CreateTablesAsync();
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

/// <summary>Coleção que compartilha um único <see cref="CoexistenciaFixture"/>.</summary>
[CollectionDefinition(Nome)]
public sealed class ColecaoCoexistencia : ICollectionFixture<CoexistenciaFixture>
{
    public const string Nome = "Coexistência Marten + EF Core";
}
