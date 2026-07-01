namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Modalidades;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

/// <summary>
/// Smoke + caminho de escrita dos endpoints de <c>Modalidade</c> (UNI-REQ-0011):
/// routing, vendor media type, HATEOAS, autenticação/autorização, idempotência,
/// domínio fechado dos enums (422), coerência de domínio (422) e unicidade do
/// código (409) — com Wolverine contra Postgres efêmero.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class ModalidadeEndpointTests
{
    private readonly ConfiguracaoEndpointFixture _fixture;

    public ModalidadeEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "GET /api/configuracao/modalidades retorna 200 com Content-Type vendor MIME")]
    public async Task Listar_Retorna200ComVendorMime()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri("/api/configuracao/modalidades", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType
            .Should().Be("application/vnd.uniplus.modalidade.v1+json");
    }

    [Fact(DisplayName = "GET /api/configuracao/modalidades/{id} retorna 404 quando inexistente")]
    public async Task ObterPorId_NaoExiste_Retorna404()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri($"/api/configuracao/modalidades/{Guid.NewGuid()}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "POST /api/configuracao/admin/modalidades sem autenticação retorna 401")]
    public async Task Criar_SemAuth_Retorna401()
    {
        using HttpClient client = _fixture.Factory.CreateDefaultClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/configuracao/admin/modalidades", UriKind.Relative));
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "POST autenticado sem role plataforma-admin retorna 403")]
    public async Task Criar_SemRoleAdmin_Retorna403()
    {
        var body = new { codigo = CodigoUnico(), naturezaLegal = "AMPLA" };

        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/configuracao/admin/modalidades", UriKind.Relative));
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
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/configuracao/admin/modalidades", UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "POST cria (201) e o GET subsequente retorna os campos + HATEOAS")]
    public async Task Criar_ComAuthEIdempotency_Retorna201EPersiste()
    {
        string codigo = CodigoUnico();
        var body = new
        {
            codigo,
            descricao = "Cota PPI de baixa renda",
            naturezaLegal = "COTA_RESERVADA",
            composicaoVagas = "DENTRO_DO_VR",
            regraRemanejamento = "SEGUE_CASCATA",
            baseLegal = "Lei 12.711/2012",
        };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarPostAdmin(client, body);

        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();
        id.Should().NotBe(Guid.Empty);

        HttpResponseMessage obter = await client.GetAsync(
            new Uri($"/api/configuracao/modalidades/{id}", UriKind.Relative));
        obter.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument doc = JsonDocument.Parse(await obter.Content.ReadAsStringAsync());
        JsonElement root = doc.RootElement;
        root.GetProperty("codigo").GetString().Should().Be(codigo);
        root.GetProperty("naturezaLegal").GetString().Should().Be("COTA_RESERVADA");
        root.GetProperty("composicaoVagas").GetString().Should().Be("DENTRO_DO_VR");
        root.GetProperty("regraRemanejamento").GetString().Should().Be("SEGUE_CASCATA");
        root.TryGetProperty("_links", out _).Should().BeTrue("HATEOAS Level 1 expõe _links.self (ADR-0029)");
    }

    [Fact(DisplayName = "POST com natureza fora do domínio fechado retorna 422")]
    public async Task Criar_NaturezaInvalida_Retorna422()
    {
        var body = new { codigo = CodigoUnico(), naturezaLegal = "COTA" };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPostAdmin(client, body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "POST RETIRA_DE sem origem retorna 422 (coerência de domínio)")]
    public async Task Criar_RetiraDeSemOrigem_Retorna422()
    {
        var body = new { codigo = CodigoUnico(), naturezaLegal = "AMPLA", composicaoVagas = "RETIRA_DE" };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPostAdmin(client, body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "POST com código já existente entre vivos retorna 409")]
    public async Task Criar_CodigoDuplicado_Retorna409()
    {
        string codigo = CodigoUnico();
        var body = new { codigo, naturezaLegal = "AMPLA" };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage primeiro = await EnviarPostAdmin(client, body);
        primeiro.StatusCode.Should().Be(HttpStatusCode.Created);

        HttpResponseMessage segundo = await EnviarPostAdmin(client, new { codigo, naturezaLegal = "AMPLA" });
        segundo.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private static string CodigoUnico() => $"MOD_{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}";

    private static async Task<HttpResponseMessage> EnviarPostAdmin(HttpClient client, object body)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/configuracao/admin/modalidades", UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }
}
