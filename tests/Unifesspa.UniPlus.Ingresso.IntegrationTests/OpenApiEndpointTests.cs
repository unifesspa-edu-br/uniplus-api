namespace Unifesspa.UniPlus.Ingresso.IntegrationTests;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Infrastructure;

using Unifesspa.UniPlus.IntegrationTests.Fixtures;

/// <summary>
/// Smoke + drift check do endpoint <c>/openapi/ingresso.json</c>:
/// runtime gera spec OpenAPI 3.x do módulo Ingresso e compara com o baseline
/// committed em <c>contracts/openapi.ingresso.json</c>. Para regerar, rodar
/// com <c>UPDATE_OPENAPI_BASELINE=1</c> definido (story #290).
/// </summary>
public sealed class OpenApiEndpointTests : IClassFixture<IngressoApiFactory>
{
    private const string BaselineRelativePath = "contracts/openapi.ingresso.json";

    private readonly IngressoApiFactory _factory;

    public OpenApiEndpointTests(IngressoApiFactory factory)
    {
        _factory = factory;
    }

    [Fact(DisplayName = "GET /openapi/ingresso.json retorna 200 com documento OpenAPI válido e metadata Uni+")]
    public async Task GetOpenApiDocument_RetornaSpecJsonComMetadataInjetada()
    {
        using HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(new Uri("/openapi/ingresso.json", UriKind.Relative));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        root.GetProperty("openapi").GetString().Should().StartWith("3.");
        root.GetProperty("info").GetProperty("title").GetString().Should().Be("Uni+ — Módulo Ingresso");
        root.GetProperty("info").GetProperty("version").GetString().Should().Be("1.0.0");
    }

    [Fact(DisplayName = "Spec runtime de Ingresso bate com baseline committed em contracts/")]
    public async Task SpecRuntime_DeveCasarComBaselineCommitted()
    {
        using HttpClient client = _factory.CreateClient();

        await BaselineDoOpenApi.ConferirAsync(client, "/openapi/ingresso.json", BaselineRelativePath);
    }
}
