using AwesomeAssertions;
using Npgsql;
using Unifesspa.UniPlus.Spikes.EventSourcing.Coexistencia;
using Xunit;

namespace Unifesspa.UniPlus.Spikes.EventSourcing.Tests;

/// <summary>
/// Gate G2 host-level — passo 1: a configuração dual-store (Marten
/// <c>IntegrateWithWolverine</c> + outbox EF Core <c>PersistMessagesWithPostgresql</c>)
/// compõe e sobe num único host, provisionando os três planos no mesmo PostgreSQL.
/// </summary>
[Collection(ColecaoCoexistencia.Nome)]
public sealed class CoexistenciaBootTests(CoexistenciaFixture fixture)
{
    [Fact(DisplayName = "G2 host-level: host único sobe com Marten + EF Core e provisiona os schemas")]
    public async Task Host_unico_sobe_com_os_dois_stores()
    {
        IReadOnlyList<string> schemas = await ListarSchemasAsync();

        schemas.Should().Contain(ConfiguracaoCoexistencia.SchemaEventosEs,
            "o Event Store ancillary do Marten provisiona seu schema");
        schemas.Should().Contain(ConfiguracaoCoexistencia.SchemaMensageria,
            "o store 'main' (outbox EF Core) provisiona seu schema de mensageria");
        schemas.Should().Contain("crud",
            "o módulo CRUD (EF Core) provisiona seu schema");
    }

    private async Task<IReadOnlyList<string>> ListarSchemasAsync()
    {
        await using NpgsqlConnection conexao = new(fixture.ConnectionString);
        await conexao.OpenAsync();

        await using NpgsqlCommand cmd = conexao.CreateCommand();
        cmd.CommandText = "SELECT schema_name FROM information_schema.schemata;";

        List<string> nomes = [];
        await using NpgsqlDataReader leitor = await cmd.ExecuteReaderAsync();
        while (await leitor.ReadAsync())
        {
            nomes.Add(leitor.GetString(0));
        }

        return nomes;
    }
}
