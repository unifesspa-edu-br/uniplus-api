namespace Unifesspa.UniPlus.Geo.IntegrationTests;

using System.Net;

using AwesomeAssertions;

using Infrastructure;

/// <summary>
/// Prova que a API do módulo Geo sobe e fica pronta contra um Postgres com
/// PostGIS provisionado (CA-02): o schema é migrado no startup (extensão postgis
/// + tabela-sonda) e <c>/health/ready</c> responde 200.
/// </summary>
[Collection(GeoPostgisCollection.Name)]
public sealed class GeoFundacaoTests
{
    private readonly GeoPostgisFixture _fixture;

    public GeoFundacaoTests(GeoPostgisFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "GET /health/ready responde 200 contra Postgres + PostGIS provisionado")]
    public async Task HealthReady_ContraPostgis_Responde200()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
