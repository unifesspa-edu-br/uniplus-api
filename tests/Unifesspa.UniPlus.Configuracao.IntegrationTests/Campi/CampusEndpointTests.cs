namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Campi;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

/// <summary>
/// Smoke + caminho de escrita dos endpoints de <c>Campus</c> (UNI-REQ #587):
/// routing, vendor media type, HATEOAS, autenticação/autorização, idempotência e
/// validação de formato da cidade (CA-03) com Wolverine rodando contra Postgres
/// efêmero.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class CampusEndpointTests
{
    private readonly ConfiguracaoEndpointFixture _fixture;

    public CampusEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "GET /api/configuracao/campi retorna 200 com Content-Type vendor MIME de campus")]
    public async Task Listar_Retorna200ComVendorMime()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(new Uri("/api/configuracao/campi", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType
            .Should().Be("application/vnd.uniplus.campus.v1+json");
    }

    [Fact(DisplayName = "GET /api/configuracao/campi/{id} retorna 404 quando inexistente")]
    public async Task ObterPorId_NaoExiste_Retorna404()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri($"/api/configuracao/campi/{Guid.NewGuid()}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "POST /api/configuracao/admin/campi sem autenticação retorna 401")]
    public async Task Criar_SemAuth_Retorna401()
    {
        using HttpClient client = _fixture.Factory.CreateDefaultClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/configuracao/admin/campi", UriKind.Relative));
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "POST /api/configuracao/admin/campi autenticado sem role plataforma-admin retorna 403")]
    public async Task Criar_SemRoleAdmin_Retorna403()
    {
        var body = new
        {
            sigla = $"C{Guid.NewGuid().ToString("N")[..6]}",
            nome = "Campus Sem Permissão",
            cidadeCodigoIbge = "1504208",
            cidadeNome = "Marabá",
            cidadeUf = "PA",
        };

        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/configuracao/admin/campi", UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "candidato"); // role insuficiente
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(body);

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "a policy [Authorize(Roles = \"plataforma-admin\")] nega um principal autenticado sem o role");
    }

    [Fact(DisplayName = "POST /api/configuracao/admin/campi sem Idempotency-Key retorna 400")]
    public async Task Criar_SemIdempotencyKey_Retorna400()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/configuracao/admin/campi", UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "POST /api/configuracao/admin/campi cria (201) e o GET subsequente retorna a cidade por código + display cache")]
    public async Task Criar_ComAuthEIdempotency_Retorna201EPersisteCidade()
    {
        string sigla = $"C{Guid.NewGuid().ToString("N")[..6]}";
        var body = new
        {
            sigla,
            nome = "Campus de Teste",
            cidadeCodigoIbge = "1504208",
            cidadeNome = "Marabá",
            cidadeUf = "PA",
        };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarPostAdmin(client, "/api/configuracao/admin/campi", body);

        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();
        id.Should().NotBe(Guid.Empty);

        HttpResponseMessage obter = await client.GetAsync(new Uri($"/api/configuracao/campi/{id}", UriKind.Relative));
        obter.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument doc = JsonDocument.Parse(await obter.Content.ReadAsStringAsync());
        JsonElement root = doc.RootElement;
        JsonElement cidade = root.GetProperty("cidade");
        cidade.GetProperty("codigoIbge").GetString().Should().Be("1504208");
        cidade.GetProperty("nome").GetString().Should().Be("Marabá");
        cidade.GetProperty("uf").GetString().Should().Be("PA");
        cidade.GetProperty("origem").GetString().Should().Be("geo-api");
        root.TryGetProperty("_links", out _).Should().BeTrue("HATEOAS Level 1 expõe _links.self (ADR-0029)");
    }

    [Fact(DisplayName = "CA-02: POST com endereço aninhado retorna endereco estruturado no GET")]
    public async Task Criar_ComEnderecoAninhado_RetornaEnderecoNoGet()
    {
        string sigla = $"C{Guid.NewGuid().ToString("N")[..6]}";
        var body = new
        {
            sigla,
            nome = "Campus com Endereço",
            cidadeCodigoIbge = "1504208",
            cidadeNome = "Marabá",
            cidadeUf = "PA",
            endereco = new
            {
                cep = "68507590",
                logradouro = "Folha 31, Quadra 7",
                numero = "s/n",
                bairro = "Nova Marabá",
                cidade = new { codigoIbge = "1504208", nome = "Marabá", uf = "PA" },
                latitude = -5.368m,
                longitude = -49.118m,
                nivelResolucao = "logradouro",
                origem = "logradouro",
            },
        };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarPostAdmin(client, "/api/configuracao/admin/campi", body);
        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();

        HttpResponseMessage obter = await client.GetAsync(new Uri($"/api/configuracao/campi/{id}", UriKind.Relative));
        using JsonDocument doc = JsonDocument.Parse(await obter.Content.ReadAsStringAsync());
        JsonElement endereco = doc.RootElement.GetProperty("endereco");
        endereco.GetProperty("cep").GetString().Should().Be("68507590");
        endereco.GetProperty("logradouro").GetString().Should().Be("Folha 31, Quadra 7");
        endereco.GetProperty("cidade").GetProperty("codigoIbge").GetString().Should().Be("1504208");
        endereco.GetProperty("nivelResolucao").GetString().Should().Be("logradouro");
    }

    [Fact(DisplayName = "CA-04: POST com endereço de cidade incoerente retorna 422")]
    public async Task Criar_EnderecoCidadeIncoerente_Retorna422()
    {
        var body = new
        {
            sigla = $"C{Guid.NewGuid().ToString("N")[..6]}",
            nome = "Campus Incoerente",
            cidadeCodigoIbge = "1504208",
            cidadeNome = "Marabá",
            cidadeUf = "PA",
            endereco = new
            {
                cep = "66000000",
                cidade = new { codigoIbge = "1501402", nome = "Belém", uf = "PA" },
                nivelResolucao = "cidade",
                origem = "faixa-cidade",
            },
        };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPostAdmin(client, "/api/configuracao/admin/campi", body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "CA-03: POST /api/configuracao/admin/campi com código IBGE malformado retorna 422 sem consultar o Geo")]
    public async Task Criar_CidadeMalformada_Retorna422()
    {
        var body = new
        {
            sigla = $"C{Guid.NewGuid().ToString("N")[..6]}",
            nome = "Campus Inválido",
            cidadeCodigoIbge = "150420", // 6 dígitos
            cidadeNome = "Marabá",
            cidadeUf = "PA",
        };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPostAdmin(client, "/api/configuracao/admin/campi", body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    private static async Task<HttpResponseMessage> EnviarPostAdmin(HttpClient client, string path, object body)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri(path, UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }
}
