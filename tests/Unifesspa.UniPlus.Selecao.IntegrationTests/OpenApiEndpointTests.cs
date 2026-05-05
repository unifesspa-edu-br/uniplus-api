namespace Unifesspa.UniPlus.Selecao.IntegrationTests;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Outbox.Cascading;

/// <summary>
/// Smoke test do endpoint <c>/openapi/{documentName}.json</c> exposto pelo
/// pipeline <c>AddUniPlusOpenApi("selecao", ...)</c> em <c>Program.cs</c>.
/// Garante que o documento gerado em runtime sai com a metadata Uni+ aplicada
/// pelos transformers (info, servers, contact) e que o spec é JSON válido.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
public sealed class OpenApiEndpointTests
{
    private readonly CascadingFixture _fixture;

    public OpenApiEndpointTests(CascadingFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "GET /openapi/selecao.json retorna 200 com documento OpenAPI válido e metadata Uni+")]
    public async Task GetOpenApiDocument_RetornaSpecJsonComMetadataInjetada()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(new Uri("/openapi/selecao.json", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        // OpenAPI 3.x — top-level: openapi, info, paths
        root.GetProperty("openapi").GetString().Should().StartWith("3.");
        root.GetProperty("info").GetProperty("title").GetString().Should().Be("Uni+ — Módulo Seleção");
        root.GetProperty("info").GetProperty("version").GetString().Should().Be("1.0.0");
        root.GetProperty("info").GetProperty("contact").GetProperty("email").GetString()
            .Should().Be("ctic@unifesspa.edu.br");

        // Servers injetados pelo UniPlusInfoTransformer (Produção, Homologação)
        JsonElement servers = root.GetProperty("servers");
        servers.GetArrayLength().Should().Be(2);
        servers[0].GetProperty("description").GetString().Should().Be("Produção");
        servers[1].GetProperty("description").GetString().Should().Be("Homologação");
    }
}
