namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox;

using System.Globalization;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Spike;

using Wolverine;

/// <summary>
/// SPIKE V3 — caminho canônico Wolverine: comando enviado via IMessageBus.SendAsync
/// → AutoApplyTransactions enrola SaveChanges → PublishDomainEventsFromEntityFrameworkCore
/// drena domain events → envelope persistido em wolverine.* na mesma transação.
///
/// Documenta: tabelas Wolverine que efetivamente recebem o envelope (necessário
/// para definir migration em #135).
/// </summary>
[Collection(SelecaoOutboxCollection.Name)]
public sealed class SelecaoOutboxV3Tests : IAsyncLifetime
{
    private readonly SelecaoOutboxFixture _fixture;

    public SelecaoOutboxV3Tests(SelecaoOutboxFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetStateAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task V3a_handler_via_IMessageBus_persiste_entity_e_envelope()
    {
        await using AsyncServiceScope scope = _fixture.CreateScope();
        IMessageBus bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();

        await bus.InvokeAsync(new PublicarEditalSpikeCommand(1, 2026, "Edital V3 sucesso"));

        Edital? persistido = await db.Editais.FirstOrDefaultAsync(e => e.Titulo == "Edital V3 sucesso");
        persistido.Should().NotBeNull("entity deve sobreviver ao SaveChanges no handler");
        persistido!.Status.Should().Be(StatusEdital.Publicado);

        IReadOnlyDictionary<string, long> wolverineTableCounts = await SnapshotWolverineTablesAsync(db);
        long totalEnvelopes = wolverineTableCounts.Values.Sum();

        // Documentar que tabelas existem após startup (independente do AC).
        Console.WriteLine($"[SPIKE V3a] tabelas wolverine + counts: {string.Join(", ", wolverineTableCounts.Select(kv => $"{kv.Key}={kv.Value}"))}");

        totalEnvelopes.Should().BeGreaterThan(0,
            "alguma tabela durável do Wolverine deveria conter o envelope do EditalPublicadoEvent");
    }

    [Fact]
    public async Task V3a_handler_falhando_apos_AddDomainEvent_nao_persiste_entity_nem_envelope()
    {
        await using AsyncServiceScope scope = _fixture.CreateScope();
        IMessageBus bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();

        Func<Task> act = () => bus.InvokeAsync(new FalharAposPublicarSpikeCommand(2, 2026, "Edital V3 falha"));

        await act.Should().ThrowAsync<Exception>("handler joga após AddDomainEvent + SaveChanges");

        Edital? persistido = await db.Editais.FirstOrDefaultAsync(e => e.Titulo == "Edital V3 falha");
        persistido.Should().BeNull("rollback transacional deveria descartar a entity");

        IReadOnlyDictionary<string, long> wolverineTableCounts = await SnapshotWolverineTablesAsync(db);
        long totalEnvelopes = wolverineTableCounts.Values.Sum();

        Console.WriteLine($"[SPIKE V3a falha] tabelas wolverine + counts: {string.Join(", ", wolverineTableCounts.Select(kv => $"{kv.Key}={kv.Value}"))}");

        totalEnvelopes.Should().Be(0,
            "rollback deveria descartar também o envelope — atomicidade write+evento garantida pela TX compartilhada");
    }

    private static async Task<IReadOnlyDictionary<string, long>> SnapshotWolverineTablesAsync(SelecaoDbContext db)
    {
        Dictionary<string, long> result = [];
        await db.Database.OpenConnectionAsync();
        NpgsqlConnection conn = (NpgsqlConnection)db.Database.GetDbConnection();

        List<string> tables = [];
        await using (NpgsqlCommand listCmd = conn.CreateCommand())
        {
            listCmd.CommandText =
                "SELECT table_name FROM information_schema.tables WHERE table_schema = 'wolverine' ORDER BY table_name";
            await using NpgsqlDataReader reader = await listCmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                tables.Add(reader.GetString(0));
            }
        }

        foreach (string table in tables)
        {
            await using NpgsqlCommand countCmd = conn.CreateCommand();
#pragma warning disable CA2100 // SPIKE: nome de tabela vem de information_schema (controlado).
            countCmd.CommandText = $"SELECT COUNT(*) FROM wolverine.\"{table}\"";
#pragma warning restore CA2100
            object? scalar = await countCmd.ExecuteScalarAsync();
            result[table] = scalar is null ? 0 : Convert.ToInt64(scalar, CultureInfo.InvariantCulture);
        }

        return result;
    }
}
