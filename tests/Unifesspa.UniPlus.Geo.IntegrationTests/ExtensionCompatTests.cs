namespace Unifesspa.UniPlus.Geo.IntegrationTests;

using System.Data;
using System.Data.Common;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Infrastructure;

using Unifesspa.UniPlus.Geo.Infrastructure.Persistence;

/// <summary>
/// Garante que a troca da imagem base para PostGIS (<c>postgis/postgis:18-3.6</c>)
/// não regride as extensões já usadas pelos demais bancos do Uni+ (CA-05):
/// <c>pg_trgm</c>, <c>unaccent</c>, <c>btree_gist</c> continuam criáveis — junto
/// com a própria <c>postgis</c>.
/// </summary>
[Collection(GeoPostgisCollection.Name)]
public sealed class ExtensionCompatTests
{
    private readonly GeoPostgisFixture _fixture;

    public ExtensionCompatTests(GeoPostgisFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Imagem PostGIS mantém pg_trgm/unaccent/btree_gist criáveis (sem regressão)")]
    public async Task ImagemPostgis_MantemExtensoesPadrao()
    {
        await using GeoDbContext context = _fixture.CreateDbContext();

        // SQL literal (sem interpolação) — criação como superusuário do container.
        await context.Database.ExecuteSqlRawAsync("CREATE EXTENSION IF NOT EXISTS pg_trgm;");
        await context.Database.ExecuteSqlRawAsync("CREATE EXTENSION IF NOT EXISTS unaccent;");
        await context.Database.ExecuteSqlRawAsync("CREATE EXTENSION IF NOT EXISTS btree_gist;");
        await context.Database.ExecuteSqlRawAsync("CREATE EXTENSION IF NOT EXISTS postgis;");

        long presentes = await ContarExtensoesAsync(context);

        presentes.Should().Be(4,
            "as 4 extensões (pg_trgm, unaccent, btree_gist, postgis) devem estar presentes na imagem PostGIS.");
    }

    private static async Task<long> ContarExtensoesAsync(GeoDbContext context)
    {
        DbConnection connection = context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using DbCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM pg_extension WHERE extname IN ('pg_trgm', 'unaccent', 'btree_gist', 'postgis');";

        object? result = await command.ExecuteScalarAsync();
        return Convert.ToInt64(result, System.Globalization.CultureInfo.InvariantCulture);
    }
}
