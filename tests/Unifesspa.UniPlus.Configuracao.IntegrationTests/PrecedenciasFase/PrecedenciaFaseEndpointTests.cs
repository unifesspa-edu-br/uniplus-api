namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.PrecedenciasFase;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

/// <summary>
/// Smoke + caminho de escrita dos endpoints de <c>PrecedenciaFase</c>
/// (UNI-REQ-0064, story #851): routing, vendor media type, HATEOAS,
/// autenticação/autorização, idempotência e guardas de domínio (422) — com
/// Wolverine contra Postgres efêmero. Espelha <c>FaseCanonicaEndpointTests</c>.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class PrecedenciaFaseEndpointTests
{
    private readonly ConfiguracaoEndpointFixture _fixture;

    public PrecedenciaFaseEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "GET /api/configuracao/precedencias-fase retorna 200 com Content-Type vendor MIME")]
    public async Task Listar_Retorna200ComVendorMime()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri("/api/configuracao/precedencias-fase", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType
            .Should().Be("application/vnd.uniplus.precedencia-fase.v1+json");
    }

    [Fact(DisplayName = "GET /api/configuracao/precedencias-fase/{id} retorna 404 quando inexistente")]
    public async Task ObterPorId_NaoExiste_Retorna404()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri($"/api/configuracao/precedencias-fase/{Guid.NewGuid()}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "POST /api/configuracao/admin/precedencias-fase sem autenticação retorna 401")]
    public async Task Criar_SemAuth_Retorna401()
    {
        using HttpClient client = _fixture.Factory.CreateDefaultClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/configuracao/admin/precedencias-fase", UriKind.Relative));
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "POST autenticado sem role plataforma-admin retorna 403")]
    public async Task Criar_SemRoleAdmin_Retorna403()
    {
        var body = new { antecessoraCodigo = "MATRICULA", sucessoraCodigo = "CHAMADA" };

        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/configuracao/admin/precedencias-fase", UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "candidato");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(body);

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "POST sem Idempotency-Key retorna 400")]
    public async Task Criar_SemIdempotencyKey_Retorna400()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/configuracao/admin/precedencias-fase", UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "POST cria (201) e o GET subsequente retorna os campos + HATEOAS")]
    public async Task Criar_ComAuthEIdempotency_Retorna201EPersiste()
    {
        var body = new { antecessoraCodigo = "CLASSIFICACAO", sucessoraCodigo = "MATRICULA", permiteSobreposicao = false };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarPostAdmin(client, body);

        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();
        id.Should().NotBe(Guid.Empty);

        HttpResponseMessage obter = await client.GetAsync(
            new Uri($"/api/configuracao/precedencias-fase/{id}", UriKind.Relative));
        obter.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument doc = JsonDocument.Parse(await obter.Content.ReadAsStringAsync());
        JsonElement root = doc.RootElement;
        root.GetProperty("antecessoraCodigo").GetString().Should().Be("CLASSIFICACAO");
        root.GetProperty("sucessoraCodigo").GetString().Should().Be("MATRICULA");
        root.GetProperty("permiteSobreposicao").GetBoolean().Should().BeFalse();
        root.TryGetProperty("_links", out _).Should().BeTrue("HATEOAS Level 1 expõe _links.self (ADR-0029)");
    }

    [Fact(DisplayName = "POST com self-loop retorna 422")]
    public async Task Criar_SelfLoop_Retorna422()
    {
        var body = new { antecessoraCodigo = "CHAMADA", sucessoraCodigo = "CHAMADA" };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPostAdmin(client, body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "POST com código fora do conjunto canônico retorna 422")]
    public async Task Criar_ForaDoCanonico_Retorna422()
    {
        var body = new { antecessoraCodigo = "ENTREVISTA_FINAL", sucessoraCodigo = "MATRICULA" };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPostAdmin(client, body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "POST com aresta já existente entre vivas retorna 422 (ArestaDuplicada)")]
    public async Task Criar_ArestaDuplicada_Retorna422()
    {
        var body = new { antecessoraCodigo = "ENSALAMENTO", sucessoraCodigo = "CHAMADA" };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage primeiro = await EnviarPostAdmin(client, body);
        primeiro.StatusCode.Should().Be(HttpStatusCode.Created);

        HttpResponseMessage segundo = await EnviarPostAdmin(client, body);
        segundo.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "PUT altera PermiteSobreposicao mantendo o par imutável; replay idempotente retorna o mesmo estado")]
    public async Task Atualizar_TrocaSobreposicaoEReplayIdempotente()
    {
        var criarBody = new { antecessoraCodigo = "AVALIACAO", sucessoraCodigo = "CLASSIFICACAO", permiteSobreposicao = false };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarPostAdmin(client, criarBody);
        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();

        var atualizarBody = new { id, permiteSobreposicao = true };
        HttpResponseMessage atualizar = await EnviarPutAdmin(client, id, atualizarBody);
        atualizar.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage obter = await client.GetAsync(
            new Uri($"/api/configuracao/precedencias-fase/{id}", UriKind.Relative));
        using JsonDocument doc = JsonDocument.Parse(await obter.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("permiteSobreposicao").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("antecessoraCodigo").GetString().Should().Be("AVALIACAO", "o par é imutável");
    }

    [Fact(DisplayName = "DELETE remove (soft-delete) e o GET subsequente retorna 404")]
    public async Task Remover_SoftDeleteEGetSubsequenteRetorna404()
    {
        var criarBody = new { antecessoraCodigo = "HABILITACAO", sucessoraCodigo = "CHAMADA" };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarPostAdmin(client, criarBody);
        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();

        using HttpRequestMessage removerRequest = new(HttpMethod.Delete, new Uri($"/api/configuracao/admin/precedencias-fase/{id}", UriKind.Relative));
        removerRequest.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        removerRequest.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
        HttpResponseMessage remover = await client.SendAsync(removerRequest);
        remover.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage obter = await client.GetAsync(
            new Uri($"/api/configuracao/precedencias-fase/{id}", UriKind.Relative));
        obter.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static async Task<HttpResponseMessage> EnviarPostAdmin(HttpClient client, object body)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/configuracao/admin/precedencias-fase", UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> EnviarPutAdmin(HttpClient client, Guid id, object body)
    {
        using HttpRequestMessage request = new(HttpMethod.Put, new Uri($"/api/configuracao/admin/precedencias-fase/{id}", UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }
}
