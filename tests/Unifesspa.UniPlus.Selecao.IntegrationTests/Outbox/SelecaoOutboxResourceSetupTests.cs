namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

[Collection(SelecaoOutboxCollection.Name)]
public sealed class SelecaoOutboxResourceSetupTests
{
    private readonly SelecaoOutboxFixture _fixture;

    public SelecaoOutboxResourceSetupTests(SelecaoOutboxFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Apos_startup_schema_wolverine_existe()
    {
        await using AsyncServiceScope scope = _fixture.CreateScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();

        bool schemaExists = await SchemaExistsAsync(db, "wolverine");

        schemaExists.Should().BeTrue(
            "AddResourceSetupOnStartup deveria provisionar o schema 'wolverine' no startup do host");
    }

    [Theory]
    [InlineData("wolverine_outgoing_envelopes")]
    [InlineData("wolverine_incoming_envelopes")]
    public async Task Apos_startup_tabela_de_outbox_existe(string tableName)
    {
        await using AsyncServiceScope scope = _fixture.CreateScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();

        bool exists = await TableExistsAsync(db, "wolverine", tableName);

        exists.Should().BeTrue(
            $"PersistMessagesWithPostgresql deveria criar wolverine.{tableName} via ResourceSetupOnStartup");
    }

    private static async Task<bool> SchemaExistsAsync(SelecaoDbContext db, string schema)
    {
        await db.Database.OpenConnectionAsync();
        await using NpgsqlCommand cmd = ((NpgsqlConnection)db.Database.GetDbConnection()).CreateCommand();
        cmd.CommandText = "SELECT 1 FROM information_schema.schemata WHERE schema_name = @schema";
        cmd.Parameters.AddWithValue("schema", schema);
        object? result = await cmd.ExecuteScalarAsync();
        return result is not null and not DBNull;
    }

    private static async Task<bool> TableExistsAsync(SelecaoDbContext db, string schema, string table)
    {
        await db.Database.OpenConnectionAsync();
        await using NpgsqlCommand cmd = ((NpgsqlConnection)db.Database.GetDbConnection()).CreateCommand();
        cmd.CommandText =
            "SELECT 1 FROM information_schema.tables WHERE table_schema = @schema AND table_name = @table";
        cmd.Parameters.AddWithValue("schema", schema);
        cmd.Parameters.AddWithValue("table", table);
        object? result = await cmd.ExecuteScalarAsync();
        return result is not null and not DBNull;
    }
}
