namespace Unifesspa.UniPlus.Geo.IntegrationTests.Etl;

using AwesomeAssertions;

using NetTopologySuite.Geometries;

using Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Etl.Parsing;

/// <summary>
/// <see cref="PontoFactory"/>: materializa lat/long em <see cref="Point"/> SRID 4326,
/// ordem PostGIS X=longitude / Y=latitude; ausência de qualquer componente → null.
/// </summary>
public sealed class PontoFactoryTests
{
    [Fact(DisplayName = "lat/long válidos viram Point 4326 com X=longitude e Y=latitude")]
    public void Criar_LatLongValidos_PontoComOrdemXLonYLat()
    {
        Point? ponto = PontoFactory.Criar("-23.55068", "-46.63413");

        ponto.Should().NotBeNull();
        ponto!.SRID.Should().Be(4326);
        ponto.X.Should().Be(-46.63413);
        ponto.Y.Should().Be(-23.55068);
    }

    [Theory]
    [InlineData(null, "-46.63413")]
    [InlineData("-23.55068", null)]
    [InlineData("-", "-")]
    [InlineData("abc", "-46.63413")]
    [InlineData("", "")]
    public void Criar_ComponenteAusenteOuInvalido_RetornaNull(string? latitude, string? longitude)
    {
        PontoFactory.Criar(latitude, longitude).Should().BeNull();
    }

    [Theory(DisplayName = "Coordenada fora do domínio geográfico degrada para null (não estoura no geography)")]
    [InlineData("999", "999")]
    [InlineData("91", "-46.6")]
    [InlineData("-90.5", "0")]
    [InlineData("-23.5", "181")]
    [InlineData("0", "-180.5")]
    public void Criar_ForaDoDominioGeografico_RetornaNull(string latitude, string longitude)
    {
        PontoFactory.Criar(latitude, longitude).Should().BeNull();
    }
}
