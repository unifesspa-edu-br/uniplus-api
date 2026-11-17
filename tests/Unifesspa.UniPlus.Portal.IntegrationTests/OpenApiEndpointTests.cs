namespace Unifesspa.UniPlus.Portal.IntegrationTests;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.IntegrationTests.Fixtures;
using Unifesspa.UniPlus.Portal.IntegrationTests.Infrastructure;

/// <summary>
/// Confere o endpoint <c>/openapi/portal.json</c>: a API em execução gera a especificação OpenAPI 3.x
/// da Portal API, e o teste a compara com o contrato gravado em <c>contracts/openapi.portal.json</c> (ADR-0030).
/// </summary>
public sealed class OpenApiEndpointTests : IClassFixture<PortalApiFactory>
{
    private const string CaminhoDoContratoGravado = "contracts/openapi.portal.json";

    private readonly PortalApiFactory _factory;

    public OpenApiEndpointTests(PortalApiFactory factory)
    {
        _factory = factory;
    }

    [Fact(DisplayName = "GET /openapi/portal.json retorna 200 com documento OpenAPI válido e metadata Uni+")]
    public async Task GetOpenApiDocument_RetornaSpecJsonComMetadataInjetada()
    {
        using HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(new Uri("/openapi/portal.json", UriKind.Relative));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        root.GetProperty("openapi").GetString().Should().StartWith("3.");
        root.GetProperty("info").GetProperty("version").GetString().Should().Be("1.0.0");
    }

    [Fact(DisplayName = "A especificação em execução da Portal API é igual ao contrato gravado em contracts/")]
    public async Task SpecRuntime_DeveCasarComContratoGravado()
    {
        using HttpClient client = _factory.CreateClient();

        await ContratoOpenApiGravado.ConferirAsync(client, "/openapi/portal.json", CaminhoDoContratoGravado);
    }
}
