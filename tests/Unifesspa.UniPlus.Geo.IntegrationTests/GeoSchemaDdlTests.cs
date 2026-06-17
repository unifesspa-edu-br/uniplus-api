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

    [Fact(DisplayName = "ix_cidade_coordenada é GIST e ix_cidade_nome_normalizado_trgm é GIN trigram")]
    public async Task IndicesCidade_GistETrgm()
    {
        string? gist = await ObterIndexDefAsync("ix_cidade_coordenada");
        gist.Should().NotBeNull();
        gist!.Should().Contain("USING gist");

        string? trgm = await ObterIndexDefAsync("ix_cidade_nome_normalizado_trgm");
        trgm.Should().NotBeNull("o índice trigram de cidade.nome_normalizado deve existir");
        trgm!.Should().Contain("USING gin", "autocomplete usa GIN");
        trgm.Should().Contain("gin_trgm_ops", "o opclass trigram precisa estar presente (extensão pg_trgm)");
    }

    [Fact(DisplayName = "extensão pg_trgm está instalada (sustenta o índice trigram)")]
    public async Task ExtensaoPgTrgm_Instalada()
    {
        await using GeoDbContext context = _fixture.CreateDbContext();
        DbConnection connection = context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using DbCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM pg_extension WHERE extname = 'pg_trgm';";
        long presentes = Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);

        presentes.Should().Be(1, "a migration deve criar a extensão pg_trgm");
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
