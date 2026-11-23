namespace Unifesspa.UniPlus.Autorizacao.IntegrationTests;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures;

/// <summary>
/// Smoke + drift check do endpoint <c>/openapi/autorizacao.json</c>:
/// runtime gera spec OpenAPI 3.x do módulo Autorização e compara
/// com o baseline committed em <c>contracts/openapi.autorizacao.json</c>.
/// </summary>
public sealed class OpenApiEndpointTests : IClassFixture<AutorizacaoApiFactory>
{
    private const string BaselineRelativePath = "contracts/openapi.autorizacao.json";

    private readonly AutorizacaoApiFactory _factory;

    public OpenApiEndpointTests(AutorizacaoApiFactory factory)
    {
        _factory = factory;
    }

    [Fact(DisplayName = "GET /openapi/autorizacao.json retorna 200 com documento OpenAPI válido e metadata Uni+")]
    public async Task GetOpenApiDocument_RetornaSpecJsonComMetadataInjetada()
    {
        using HttpResponseMessage response = await ObterDocumentoAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        root.GetProperty("openapi").GetString().Should().StartWith("3.");
        root.GetProperty("info").GetProperty("title").GetString().Should().Be("Uni+ — Módulo Autorização");
        root.GetProperty("info").GetProperty("version").GetString().Should().Be("1.0.0");
    }

    [Fact(DisplayName = "O componente dos códigos da situação do acesso explica que a situação é do perfil, e não de cada concessão")]
    public async Task SituacaoDoAcesso_ExplicaQueEDoPerfilENaoDaConcessao()
    {
        using HttpResponseMessage response = await ObterDocumentoAsync();
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        JsonElement componente = doc.RootElement.GetProperty("components").GetProperty("schemas").GetProperty("SituacaoDoAcessoDoPerfilCodigo");

        componente.GetProperty("description").GetString().Should().Contain("não de cada concessão");
    }

    [Fact(DisplayName = "Spec runtime de Autorização bate com baseline committed em contracts/")]
    public async Task SpecRuntime_DeveCasarComBaselineCommitted()
    {
        using HttpClient client = _factory.CreateClient();
        await ContratoOpenApiGravado.ConferirAsync(client, "/openapi/autorizacao.json", BaselineRelativePath);
    }

    private async Task<HttpResponseMessage> ObterDocumentoAsync()
    {
        using HttpClient client = _factory.CreateClient();
        return await client.GetAsync(new Uri("/openapi/autorizacao.json", UriKind.Relative));
    }

}
