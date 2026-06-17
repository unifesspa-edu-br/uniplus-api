namespace Unifesspa.UniPlus.Geo.IntegrationTests;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using NetTopologySuite.Geometries;

using Infrastructure;

using Unifesspa.UniPlus.Geo.Domain.Entities;
using Unifesspa.UniPlus.Geo.Infrastructure.Persistence;

/// <summary>
/// Prova fim-a-fim o mapeamento <c>geography(Point,4326)</c> via NetTopologySuite
/// (CA-03): um <see cref="Point"/> SRID 4326 é persistido e relido contra o
/// PostGIS real, preservando a coordenada e o SRID.
/// </summary>
[Collection(GeoPostgisCollection.Name)]
public sealed class PostGisPersistenceTests
{
    private readonly GeoPostgisFixture _fixture;

    public PostGisPersistenceTests(GeoPostgisFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Point SRID 4326 persiste e relê via NetTopologySuite (geography fim-a-fim)")]
    public async Task PersisteELe_Point4326()
    {
        // Coordenada aproximada de Marabá-PA (longitude X, latitude Y).
        const double longitude = -49.1278;
        const double latitude = -5.3686;
        Point coordenada = new(longitude, latitude) { SRID = 4326 };
        PontoReferenciaSonda sonda = PontoReferenciaSonda.Criar(coordenada);

        await using (GeoDbContext gravacao = _fixture.CreateDbContext())
        {
            gravacao.PontosReferenciaSonda.Add(sonda);
            await gravacao.SaveChangesAsync();
        }

        await using GeoDbContext leitura = _fixture.CreateDbContext();
        PontoReferenciaSonda lido = await leitura.PontosReferenciaSonda
            .SingleAsync(p => p.Id == sonda.Id);

        lido.Coordenada.X.Should().BeApproximately(longitude, 1e-6);
        lido.Coordenada.Y.Should().BeApproximately(latitude, 1e-6);
        lido.Coordenada.SRID.Should().Be(4326);
    }
}
