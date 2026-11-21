namespace Unifesspa.UniPlus.Configuracao.IntegrationTests;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Infrastructure;

using Unifesspa.UniPlus.IntegrationTests.Fixtures;

/// <summary>
/// Smoke + drift check do endpoint <c>/openapi/configuracao.json</c>: runtime
/// gera o spec OpenAPI 3.x do módulo Configuração e compara com o baseline
/// committed em <c>contracts/openapi.configuracao.json</c> (ADR-0030).
/// </summary>
public sealed class OpenApiEndpointTests : IClassFixture<ConfiguracaoApiFactory>
{
    private const string BaselineRelativePath = "contracts/openapi.configuracao.json";

    private readonly ConfiguracaoApiFactory _factory;

    public OpenApiEndpointTests(ConfiguracaoApiFactory factory)
    {
        _factory = factory;
    }

    [Fact(DisplayName = "GET /openapi/configuracao.json retorna 200 com documento OpenAPI válido e metadata Uni+")]
    public async Task GetOpenApiDocument_RetornaSpecJsonComMetadataInjetada()
    {
        using HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(new Uri("/openapi/configuracao.json", UriKind.Relative));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        root.GetProperty("openapi").GetString().Should().StartWith("3.");
        root.GetProperty("info").GetProperty("version").GetString().Should().Be("1.0.0");
    }

    [Fact(DisplayName = "Spec runtime de Configuração bate com baseline committed em contracts/")]
    public async Task SpecRuntime_DeveCasarComBaselineCommitted()
    {
        using HttpClient client = _factory.CreateClient();

        await ContratoOpenApiGravado.ConferirAsync(client, "/openapi/configuracao.json", BaselineRelativePath);
    }

    [Fact(DisplayName = "No contrato da Configuração, todo campo de texto que leva o código de um enum referencia o componente dos códigos")]
    public async Task CampoDeTextoComCodigoDeEnum_ReferenciaOComponenteDosCodigos()
    {
        using HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync(new Uri("/openapi/configuracao.json", UriKind.Relative));
        response.EnsureSuccessStatusCode();
        using JsonDocument spec = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        CodigosDeEnumNoContrato.CamposComTextoCru(spec.RootElement).Should().BeEmpty(
            "o campo que leva código de enum de contrato publica o componente dos códigos do enum (ADR-0176)");
        CodigosDeEnumNoContrato.ReferenciaOsCodigos(spec.RootElement, "CondicaoDoPredicadoDto", "operador").Should().BeTrue();
        CodigosDeEnumNoContrato.ReferenciaOsCodigos(spec.RootElement, "ApresentacaoDoCampoDto", "tipoRenderizacao").Should().BeTrue();
    }
}
