using AwesomeAssertions;
using Npgsql;
using Unifesspa.UniPlus.Spikes.EventSourcing.Infrastructure;
using Xunit;

namespace Unifesspa.UniPlus.Spikes.EventSourcing.Tests;

/// <summary>
/// Gate G2 (dimensão de armazenamento): o Event Store do Marten e o outbox durável
/// do Wolverine coabitam no mesmo banco/schema PostgreSQL — sem componente
/// operacional novo. A coabitação no MESMO host do outbox EF Core de produção é
/// uma questão de topologia tratada no relatório de findings.
/// </summary>
[Collection(ColecaoSpike.Nome)]
public sealed class CoexistenciaSchemaTests(SpikeFixture fixture)
{
    [Fact(DisplayName = "G2: eventos do Marten e envelopes do outbox Wolverine no mesmo schema")]
    public async Task EventStore_e_outbox_coabitam_no_mesmo_schema()
    {
        // Garante que o schema já foi materializado (provoca o append de ao menos 1 evento).
        await fixture.Bus.InvokeAsync(new Application.Comandos.AbrirEdital(
            Guid.CreateVersion7(), "099/2026", "Edital G2", TestHelpers.AtorFicticio()));

        IReadOnlyList<string> tabelas = await ListarTabelasAsync(ConfiguracaoSpike.SchemaEventos);

        tabelas.Should().Contain(t => t.StartsWith("mt_", StringComparison.Ordinal),
            "o Event Store do Marten cria tabelas mt_* no schema");
        tabelas.Should().Contain(t => t.Contains("envelope", StringComparison.Ordinal),
            "o outbox durável do Wolverine persiste envelopes no MESMO schema");
    }

    private async Task<IReadOnlyList<string>> ListarTabelasAsync(string schema)
    {
        await using NpgsqlConnection conexao = new(fixture.ConnectionString);
        await conexao.OpenAsync();

        await using NpgsqlCommand cmd = conexao.CreateCommand();
        cmd.CommandText = "SELECT table_name FROM information_schema.tables WHERE table_schema = @schema;";
        cmd.Parameters.AddWithValue("schema", schema);

        List<string> nomes = [];
        await using NpgsqlDataReader leitor = await cmd.ExecuteReaderAsync();
        while (await leitor.ReadAsync())
        {
            nomes.Add(leitor.GetString(0));
        }

        return nomes;
    }
}
