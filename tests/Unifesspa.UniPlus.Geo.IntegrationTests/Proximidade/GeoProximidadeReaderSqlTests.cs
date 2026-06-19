namespace Unifesspa.UniPlus.Geo.IntegrationTests.Proximidade;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using NetTopologySuite.Geometries;

using Unifesspa.UniPlus.Geo.Infrastructure.Persistence;
using Unifesspa.UniPlus.Geo.IntegrationTests.Infrastructure;

/// <summary>
/// Teste de tradução EF Core/Npgsql (CA-02): prova que o filtro de raio vira
/// <c>ST_DWithin</c> (index-friendly, usa o índice GIST) e a ordenação vira
/// <c>ST_Distance</c>. Espelha a consulta de <c>GeoProximidadeReader</c> e inspeciona
/// o SQL via <c>ToQueryString()</c> — sem executar contra o banco.
/// </summary>
[Collection(GeoPostgisCollection.Name)]
public sealed class GeoProximidadeReaderSqlTests
{
    private readonly GeoPostgisFixture _fixture;

    public GeoProximidadeReaderSqlTests(GeoPostgisFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "CA-02: filtro de raio usa ST_DWithin (GIST) no WHERE; ordenação usa ST_Distance")]
    public async Task ProximidadeQuery_TraduzParaStDWithinNoFiltroEStDistanceNaOrdenacao()
    {
        await using GeoDbContext ctx = _fixture.CreateDbContext();

        Point ponto = new(-49.13, -5.35) { SRID = 4326 };
        const double raioMetros = 100 * 1000.0;

        // Espelha GeoProximidadeReader.BuscarCidadesProximasAsync.
        var consulta = ctx.Cidades
            .AsNoTracking()
            .Where(c => c.Vigente
                     && c.Coordenada != null
                     && c.Latitude != null
                     && c.Longitude != null
                     && c.Coordenada.IsWithinDistance(ponto, raioMetros))
            .OrderBy(c => c.Coordenada!.Distance(ponto))
            .ThenBy(c => c.Id)
            .Select(c => new { c.Id, Distancia = c.Coordenada!.Distance(ponto) });

        string sql = consulta.ToQueryString();

        // Isola o predicado de filtragem (WHERE … ORDER BY): deve usar ST_DWithin
        // (index-friendly, GIST) e NÃO ST_Distance (a projeção/ordenação é que usa
        // ST_Distance, não o filtro de raio).
        int idxWhere = sql.IndexOf("WHERE", StringComparison.Ordinal);
        int idxOrderBy = sql.IndexOf("ORDER BY", StringComparison.Ordinal);
        idxWhere.Should().BeGreaterThanOrEqualTo(0);
        idxOrderBy.Should().BeGreaterThan(idxWhere);
        string whereClause = sql[idxWhere..idxOrderBy];
        whereClause.Should().Contain("ST_DWithin", "o filtro de raio deve usar ST_DWithin (índice GIST)");
        whereClause.Should().NotContain("ST_Distance", "o filtro NÃO deve usar ST_Distance no WHERE");

        // ST_Distance aparece na ordenação por distância.
        string orderByClause = sql[idxOrderBy..];
        orderByClause.Should().Contain("ST_Distance", "a ordenação por distância deve usar ST_Distance");
    }
}
