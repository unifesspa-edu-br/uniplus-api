namespace Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Unidades;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Infrastructure;

/// <summary>
/// Smoke tests dos endpoints de <c>Unidade</c> introduzidos na Story #586.
/// Verificam wiring do controller (routing, vendor media type, HATEOAS),
/// aplicação de autenticação/autorização e resposta 404 para recursos
/// inexistentes — sem dependência de banco de dados real.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit IClassFixture<T> exige tipo de teste público.")]
public sealed class UnidadeEndpointTests : IClassFixture<OrganizacaoApiFactory>
{
    private readonly OrganizacaoApiFactory _factory;

    public UnidadeEndpointTests(OrganizacaoApiFactory factory)
    {
        _factory = factory;
    }

    [Fact(DisplayName = "GET /api/unidades retorna 200 com Content-Type vendor MIME de unidade")]
    public async Task Listar_Retorna200ComVendorMime()
    {
        using HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri("/api/unidades", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType
            .Should().Be("application/vnd.uniplus.unidade.v1+json");
    }

    [Fact(DisplayName = "GET /api/unidades retorna array JSON (catálogo vazio)")]
    public async Task Listar_RetornaArrayJson()
    {
        using HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri("/api/unidades", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact(DisplayName = "GET /api/unidades/{id} retorna 404 quando unidade inexistente")]
    public async Task ObterPorId_NaoExiste_Retorna404()
    {
        using HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri($"/api/unidades/{Guid.NewGuid()}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "POST /api/admin/unidades sem Idempotency-Key retorna 400")]
    public async Task Criar_SemIdempotencyKey_Retorna400()
    {
        using HttpClient client = _factory.CreateClient();
        using StringContent content = new("{}", Encoding.UTF8, "application/json");

        // TestAuthHandler injeta user autenticado como plataforma-admin.
        HttpResponseMessage response = await client.PostAsync(
            new Uri("/api/admin/unidades", UriKind.Relative), content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "Idempotency-Key é obrigatório — sem ela o filter retorna 400");
    }

    [Fact(DisplayName = "POST /api/admin/unidades sem autenticação retorna 401")]
    public async Task Criar_SemAuth_Retorna401()
    {
        // Factory sem autenticação — CreateDefaultClient não injeta o TestAuthHandler.
        using HttpClient client = _factory.CreateDefaultClient();
        using HttpRequestMessage request = new(
            HttpMethod.Post,
            new Uri("/api/admin/unidades", UriKind.Relative));
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "DELETE /api/admin/unidades/{id} sem autenticação retorna 401")]
    public async Task Remover_SemAuth_Retorna401()
    {
        using HttpClient client = _factory.CreateDefaultClient();

        HttpResponseMessage response = await client.DeleteAsync(
            new Uri($"/api/admin/unidades/{Guid.NewGuid()}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
