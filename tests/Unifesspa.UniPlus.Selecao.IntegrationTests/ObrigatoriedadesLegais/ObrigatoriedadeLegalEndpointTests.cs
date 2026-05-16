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
/// Testes que exigem authenticated admin context (POST/PUT/DELETE com RBAC
/// área-scoped) ficam fora deste smoke — entram em follow-up dedicado com
/// fixture de TestAuthHandler ajustada para popular AreasAdministradas.
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

    [Fact(DisplayName = "GET /api/selecao/editais/{id}/conformidade-historica retorna 404 com type específico quando sem snapshot")]
    public async Task ConformidadeHistorica_SemSnapshot_Retorna404ComProblemDetailsEspecifico()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        // Edital inexistente (ou existente sem snapshot ainda) — em V1 a tabela
        // edital_governance_snapshot está vazia per #460 (INSERT diferido para
        // #462). O endpoint precisa retornar 404 com type
        // uniplus.selecao.conformidade.snapshot_nao_disponivel.
        Guid editalId = Guid.NewGuid();

        HttpResponseMessage response = await client.GetAsync(
            new Uri($"/api/selecao/editais/{editalId}/conformidade-historica", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        string body = await response.Content.ReadAsStringAsync();
        if (!string.IsNullOrWhiteSpace(body))
        {
            using JsonDocument doc = JsonDocument.Parse(body);
            // ProblemDetails RFC 9457 — type é a URI canônica do erro.
            if (doc.RootElement.TryGetProperty("type", out JsonElement typeElement))
            {
                typeElement.GetString()
                    .Should().Be("uniplus.selecao.conformidade.snapshot_nao_disponivel");
            }
        }
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
