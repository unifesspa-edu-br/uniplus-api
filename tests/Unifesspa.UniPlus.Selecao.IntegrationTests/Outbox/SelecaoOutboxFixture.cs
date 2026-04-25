namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

using Testcontainers.PostgreSql;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit IClassFixture<T> requires the fixture type to be public.")]
[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Lifecycle ownership is via xUnit IAsyncLifetime.DisposeAsync — analyzer does not recognize this contract.")]
public sealed class SelecaoOutboxFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("uniplus_outbox_tests")
        .WithUsername("uniplus")
        .WithPassword("uniplus_dev")
        .Build();

    private SelecaoOutboxApiFactory? _factory;

    public string ConnectionString => _container.GetConnectionString();

    public SelecaoOutboxApiFactory Factory =>
        _factory ?? throw new InvalidOperationException("Fixture não inicializada — InitializeAsync ainda não rodou.");

    public async Task InitializeAsync()
    {
        await _container.StartAsync().ConfigureAwait(false);
        _factory = new SelecaoOutboxApiFactory(ConnectionString);

        // Forçar build do host: dispara o pipeline do JasperFx.Resources, que
        // provisiona o schema "wolverine" e as tabelas wolverine_outgoing_envelopes
        // / wolverine_incoming_envelopes via PersistMessagesWithPostgresql.
        // Sem isso os testes só veriam o schema após o primeiro request.
        _ = _factory.Services;

        // Cria as tabelas do modelo EF (entidades de domínio) — apenas para os
        // testes; produção usa migrations (ver issue #155). Não usamos
        // EnsureCreatedAsync porque ele é all-or-nothing e o schema "wolverine"
        // já existe (provisionado pelo JasperFx.Resources acima), o que faria
        // EnsureCreated pular a criação das tabelas.
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        IRelationalDatabaseCreator creator = db.GetInfrastructure().GetRequiredService<IRelationalDatabaseCreator>();
        await creator.CreateTablesAsync().ConfigureAwait(false);
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync().ConfigureAwait(false);
        }

        await _container.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Limpa as tabelas de outbox e a tabela <c>Editais</c> entre testes para
    /// preservar isolamento. Não derruba o schema — mantém o overhead baixo.
    /// </summary>
    public async Task ResetStateAsync()
    {
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
#pragma warning disable EF1002 // Risk de SQL injection — strings literais hardcoded e nomes via information_schema.
        // Limpa todas as tabelas do schema wolverine — Wolverine 5.x pode criar
        // tabelas extras (queue persistence, dead letters, etc.) além das envelopes.
        await db.Database.ExecuteSqlRawAsync(@"
            DO $$
            DECLARE r record;
            BEGIN
                FOR r IN SELECT tablename FROM pg_tables WHERE schemaname = 'wolverine'
                LOOP
                    EXECUTE format('TRUNCATE TABLE wolverine.%I CASCADE', r.tablename);
                END LOOP;
            END $$;").ConfigureAwait(false);
        await db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE editais CASCADE").ConfigureAwait(false);
#pragma warning restore EF1002
    }

    /// <summary>
    /// Remove referências externas para evitar leak entre asserts; conveniência
    /// para testes que querem operar fora do <see cref="Edital"/> placeholder.
    /// </summary>
    public AsyncServiceScope CreateScope() => Factory.Services.CreateAsyncScope();
}
