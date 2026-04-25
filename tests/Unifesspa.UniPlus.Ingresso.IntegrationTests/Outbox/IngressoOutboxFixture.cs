namespace Unifesspa.UniPlus.Ingresso.IntegrationTests.Outbox;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Testcontainers.PostgreSql;

using Unifesspa.UniPlus.Ingresso.Infrastructure.Persistence;

[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit IClassFixture<T> requires the fixture type to be public.")]
[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Lifecycle ownership is via xUnit IAsyncLifetime.DisposeAsync — analyzer does not recognize this contract.")]
public sealed class IngressoOutboxFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("uniplus_outbox_tests")
        .WithUsername("uniplus")
        .WithPassword("uniplus_dev")
        .Build();

    private IngressoOutboxApiFactory? _factory;

    public string ConnectionString => _container.GetConnectionString();

    public IngressoOutboxApiFactory Factory =>
        _factory ?? throw new InvalidOperationException("Fixture não inicializada — InitializeAsync ainda não rodou.");

    public async Task InitializeAsync()
    {
        await _container.StartAsync().ConfigureAwait(false);
        _factory = new IngressoOutboxApiFactory(ConnectionString);

        // Forçar build do host: dispara o pipeline do JasperFx.Resources, que
        // provisiona o schema "wolverine" e as tabelas wolverine_outgoing_envelopes
        // / wolverine_incoming_envelopes via PersistMessagesWithPostgresql.
        _ = _factory.Services;

        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        IngressoDbContext db = scope.ServiceProvider.GetRequiredService<IngressoDbContext>();
        await db.Database.EnsureCreatedAsync().ConfigureAwait(false);
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync().ConfigureAwait(false);
        }

        await _container.DisposeAsync().ConfigureAwait(false);
    }

    public AsyncServiceScope CreateScope() => Factory.Services.CreateAsyncScope();
}
