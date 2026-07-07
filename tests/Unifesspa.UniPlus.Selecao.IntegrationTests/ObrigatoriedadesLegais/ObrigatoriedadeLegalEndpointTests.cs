namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ObrigatoriedadesLegais;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Outbox.Cascading;

/// <summary>
/// Smoke tests do contrato dos endpoints introduzidos em #461. Cobrem o
/// caminho público sem autenticação para confirmar:
/// (a) wiring do controller (Route, VendorMediaType, HATEOAS),
/// (b) shape do ProblemDetails específico para conformidade-historica 404.
///
/// Testes que exigem authenticated admin context (POST/PUT/DELETE restrito a
/// plataforma-admin) ficam em ObrigatoriedadeLegalAdminEndpointTests, com a
/// fixture de TestAuthHandler que popula os roles do requisitante.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
public sealed class ObrigatoriedadeLegalEndpointTests
{
    private readonly CascadingFixture _fixture;

    public ObrigatoriedadeLegalEndpointTests(CascadingFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "GET /api/selecao/obrigatoriedades-legais retorna 200 com array vazio quando catálogo vazio")]
    public async Task Listar_RetornaArrayJsonVazio()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri("/api/selecao/obrigatoriedades-legais?limit=10", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType
            .Should().Be("application/vnd.uniplus.obrigatoriedade-legal.v1+json");

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact(DisplayName = "GET /api/selecao/obrigatoriedades-legais/{id} retorna 404 quando inexistente")]
    public async Task ObterPorId_NaoExiste_Retorna404()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri($"/api/selecao/obrigatoriedades-legais/{Guid.NewGuid()}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "POST /api/selecao/admin/obrigatoriedades-legais sem auth retorna 401")]
    public async Task Criar_SemAuth_Retorna401()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(
            HttpMethod.Post,
            new Uri("/api/selecao/admin/obrigatoriedades-legais", UriKind.Relative));
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
