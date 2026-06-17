namespace Unifesspa.UniPlus.Geo.IntegrationTests;

using System.Data;
using System.Data.Common;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Infrastructure;

using Unifesspa.UniPlus.Geo.Infrastructure.Persistence;

/// <summary>
/// Verifica o DDL físico que a persistência de entidades sozinha não prova: o
/// método do índice espacial (GIST) realmente chegou ao banco. Persistir um
/// <c>Point</c> passaria mesmo sem índice; este teste inspeciona
/// <c>pg_indexes.indexdef</c> diretamente (achado de revisão).
/// </summary>
[Collection(GeoPostgisCollection.Name)]
public sealed class GeoSchemaDdlTests
{
    private readonly GeoPostgisFixture _fixture;

    public GeoSchemaDdlTests(GeoPostgisFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "ix_estado_coordenada é um índice GIST sobre a coluna geográfica")]
    public async Task IndiceCoordenadaEstado_UsaGist()
    {
        string? indexDef = await ObterIndexDefAsync("ix_estado_coordenada");

        indexDef.Should().NotBeNull("o índice espacial de estado.coordenada deve existir");
        indexDef!.Should().Contain("USING gist", "a coordenada precisa de índice GIST (ADR-0091)");
    }

    private async Task<string?> ObterIndexDefAsync(string indexName)
    {
        await using GeoDbContext context = _fixture.CreateDbContext();
        DbConnection connection = context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using DbCommand command = connection.CreateCommand();
        command.CommandText = "SELECT indexdef FROM pg_indexes WHERE indexname = @nome;";
        DbParameter parametro = command.CreateParameter();
        parametro.ParameterName = "nome";
        parametro.Value = indexName;
        command.Parameters.Add(parametro);

        object? result = await command.ExecuteScalarAsync();
        return result as string;
    }
}
