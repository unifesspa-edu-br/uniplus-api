namespace Unifesspa.UniPlus.Publicacoes.IntegrationTests;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.IntegrationTests.Fixtures;
using Unifesspa.UniPlus.Publicacoes.IntegrationTests.Infrastructure;

/// <summary>
/// Confere o endpoint <c>/openapi/publicacoes.json</c>: a API em execução gera a especificação OpenAPI 3.x
/// do módulo Publicações, e o teste a compara com o contrato gravado em <c>contracts/openapi.publicacoes.json</c> (ADR-0030).
/// </summary>
public sealed class OpenApiEndpointTests : IClassFixture<PublicacoesApiFactory>
{
    private const string CaminhoDoContratoGravado = "contracts/openapi.publicacoes.json";

    private readonly PublicacoesApiFactory _factory;

    public OpenApiEndpointTests(PublicacoesApiFactory factory)
    {
        _factory = factory;
    }

    [Fact(DisplayName = "GET /openapi/publicacoes.json retorna 200 com documento OpenAPI válido e metadata Uni+")]
    public async Task GetOpenApiDocument_RetornaSpecJsonComMetadataInjetada()
    {
        using HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(new Uri("/openapi/publicacoes.json", UriKind.Relative));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        root.GetProperty("openapi").GetString().Should().StartWith("3.");
        root.GetProperty("info").GetProperty("version").GetString().Should().Be("1.0.0");
    }

    [Fact(DisplayName = "A especificação em execução de Publicações é igual ao contrato gravado em contracts/")]
    public async Task SpecRuntime_DeveCasarComBaselineCommitted()
    {
        using HttpClient client = _factory.CreateClient();

        await ContratoOpenApiGravado.ConferirAsync(client, "/openapi/publicacoes.json", CaminhoDoContratoGravado);
    }
}
