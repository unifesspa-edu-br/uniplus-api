namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.BaseLegalBonusRegional;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

/// <summary>
/// Contrato HTTP dos 5 endpoints de <c>BaseLegalBonusRegional</c> (Story #1465): routing,
/// autenticação/autorização por permissão de manutenção, idempotência, forma dos campos
/// (município mínimo, código IBGE) e soft-delete — com Wolverine contra Postgres efêmero.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class BaseLegalBonusRegionalEndpointTests
{
    private readonly ConfiguracaoEndpointFixture _fixture;

    public BaseLegalBonusRegionalEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "GET /api/configuracao/base-legal-bonus-regional retorna 200 com vendor MIME, sem autenticação")]
    public async Task Listar_Retorna200ComVendorMime()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri("/api/configuracao/base-legal-bonus-regional", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType
            .Should().Be("application/vnd.uniplus.base-legal-bonus-regional.v1+json");
    }

    [Fact(DisplayName = "GET /api/configuracao/base-legal-bonus-regional/{id} retorna 404 quando inexistente")]
    public async Task ObterPorId_NaoExiste_Retorna404()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri($"/api/configuracao/base-legal-bonus-regional/{Guid.NewGuid()}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "POST admin sem autenticação retorna 401")]
    public async Task Criar_SemAuth_Retorna401()
    {
        using HttpClient client = _fixture.Factory.CreateDefaultClient();
        using HttpRequestMessage request = new(
            HttpMethod.Post, new Uri("/api/configuracao/admin/base-legal-bonus-regional", UriKind.Relative));
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "POST autenticado sem role plataforma-admin retorna 403")]
    public async Task Criar_SemRoleAdmin_Retorna403()
    {
        object body = MunicipioCorpo();

        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(
            HttpMethod.Post, new Uri("/api/configuracao/admin/base-legal-bonus-regional", UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "candidato");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(body);

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "a policy [Authorize(Roles = \"plataforma-admin\")] nega um principal autenticado sem o role");
    }

    [Fact(DisplayName = "POST sem Idempotency-Key retorna 400")]
    public async Task Criar_SemIdempotencyKey_Retorna400()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(
            HttpMethod.Post, new Uri("/api/configuracao/admin/base-legal-bonus-regional", UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Content = JsonContent.Create(MunicipioCorpo());

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "POST cria (201) e o GET subsequente retorna os campos e os municípios")]
    public async Task Criar_ComAuthEIdempotency_Retorna201EPersiste()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        object body = MunicipioCorpo(identificacao: "Portaria Unifesspa nº 2514/2023");

        HttpResponseMessage criar = await EnviarPostAdmin(client, body);

        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();
        id.Should().NotBe(Guid.Empty);

        HttpResponseMessage obter = await client.GetAsync(
            new Uri($"/api/configuracao/base-legal-bonus-regional/{id}", UriKind.Relative));
        obter.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument doc = JsonDocument.Parse(await obter.Content.ReadAsStringAsync());
        JsonElement root = doc.RootElement;
        root.GetProperty("tipoInstrumento").GetString().Should().Be("PORTARIA");
        root.GetProperty("identificacao").GetString().Should().Be("Portaria Unifesspa nº 2514/2023");
        root.GetProperty("municipios").GetArrayLength().Should().Be(1);
        root.GetProperty("municipios")[0].GetProperty("codigoIbge").GetString().Should().Be("1504208");
        root.TryGetProperty("_links", out _).Should().BeTrue("HATEOAS Level 1 expõe _links.self (ADR-0029)");
    }

    [Fact(DisplayName = "POST sem nenhum município retorna 422 sem gravar")]
    public async Task Criar_SemMunicipio_Retorna422()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        object body = MunicipioCorpo(municipios: []);

        HttpResponseMessage response = await EnviarPostAdmin(client, body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString()
            .Should().Be("uniplus.configuracao.base_legal_bonus_regional.sem_municipios");
    }

    [Fact(DisplayName = "POST com código IBGE fora do formato de 7 dígitos retorna 422 sem gravar")]
    public async Task Criar_ComCodigoIbgeInvalido_Retorna422()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        object body = MunicipioCorpo(municipios: [new { codigoIbge = "123", nome = "Marabá", uf = "PA" }]);

        HttpResponseMessage response = await EnviarPostAdmin(client, body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString()
            .Should().Be("uniplus.configuracao.base_legal_bonus_regional.municipio_invalido");
    }

    [Fact(DisplayName = "POST com item nulo na lista de municípios retorna 422, não 500")]
    public async Task Criar_ComMunicipioNulo_Retorna422()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        object body = MunicipioCorpo(municipios: [null!]);

        HttpResponseMessage response = await EnviarPostAdmin(client, body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "POST com caractere nulo na identificação retorna 422, não 500 (rejeição do Postgres)")]
    public async Task Criar_ComCaractereNuloNaIdentificacao_Retorna422()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        object body = MunicipioCorpo(identificacao: "Portaria\0Inválida");

        HttpResponseMessage response = await EnviarPostAdmin(client, body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString()
            .Should().Be("uniplus.configuracao.base_legal_bonus_regional.identificacao_caractere_nulo");
    }

    [Fact(DisplayName = "PUT com Id na URL divergente do corpo retorna 400")]
    public async Task Atualizar_IdDivergente_Retorna400()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid idNaUrl = Guid.NewGuid();
        var body = new
        {
            id = Guid.NewGuid(),
            tipoInstrumento = "PORTARIA",
            identificacao = "Portaria X",
            descricao = "Descricao",
            municipios = new[] { new { codigoIbge = "1504208", nome = "Marabá", uf = "PA" } },
        };

        using HttpRequestMessage request = new(
            HttpMethod.Put, new Uri($"/api/configuracao/admin/base-legal-bonus-regional/{idNaUrl}", UriKind.Relative));
        AutenticarComoAdmin(request);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(body);

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "PUT sem Idempotency-Key retorna 400")]
    public async Task Atualizar_SemIdempotencyKey_Retorna400()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = Guid.NewGuid();
        var body = new
        {
            id,
            tipoInstrumento = "PORTARIA",
            identificacao = "Portaria X",
            descricao = "Descricao",
            municipios = new[] { new { codigoIbge = "1504208", nome = "Marabá", uf = "PA" } },
        };

        using HttpRequestMessage request = new(
            HttpMethod.Put, new Uri($"/api/configuracao/admin/base-legal-bonus-regional/{id}", UriKind.Relative));
        AutenticarComoAdmin(request);
        request.Content = JsonContent.Create(body);

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "PUT em Id inexistente retorna 404")]
    public async Task Atualizar_IdInexistente_Retorna404()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = Guid.NewGuid();
        var body = new
        {
            id,
            tipoInstrumento = "PORTARIA",
            identificacao = "Portaria X",
            descricao = "Descricao",
            municipios = new[] { new { codigoIbge = "1504208", nome = "Marabá", uf = "PA" } },
        };

        using HttpRequestMessage request = new(
            HttpMethod.Put, new Uri($"/api/configuracao/admin/base-legal-bonus-regional/{id}", UriKind.Relative));
        AutenticarComoAdmin(request);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(body);

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "PUT válido atualiza os campos e substitui a lista de municípios")]
    public async Task Atualizar_ComAuthEIdempotency_Retorna204EPersiste()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarPostAdmin(client, MunicipioCorpo());
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();

        var atualizacao = new
        {
            id,
            tipoInstrumento = "PORTARIA",
            identificacao = "Portaria Unifesspa nº 2514/2023 (revisada)",
            descricao = "Descricao revisada",
            municipios = new[] { new { codigoIbge = "1501402", nome = "Belém", uf = "PA" } },
        };

        using HttpRequestMessage request = new(
            HttpMethod.Put, new Uri($"/api/configuracao/admin/base-legal-bonus-regional/{id}", UriKind.Relative));
        AutenticarComoAdmin(request);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(atualizacao);

        HttpResponseMessage response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage obter = await client.GetAsync(
            new Uri($"/api/configuracao/base-legal-bonus-regional/{id}", UriKind.Relative));
        using JsonDocument doc = JsonDocument.Parse(await obter.Content.ReadAsStringAsync());
        JsonElement root = doc.RootElement;
        root.GetProperty("identificacao").GetString().Should().Be("Portaria Unifesspa nº 2514/2023 (revisada)");
        root.GetProperty("municipios").GetArrayLength().Should().Be(1);
        root.GetProperty("municipios")[0].GetProperty("codigoIbge").GetString().Should().Be("1501402");
    }

    [Fact(DisplayName = "DELETE inexistente retorna 404")]
    public async Task Remover_Inexistente_Retorna404()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(
            HttpMethod.Delete, new Uri($"/api/configuracao/admin/base-legal-bonus-regional/{Guid.NewGuid()}", UriKind.Relative));
        AutenticarComoAdmin(request);

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "DELETE desativa (soft-delete): some do GET por id e não é mais recuperável")]
    public async Task Remover_ComAuth_FazSoftDelete()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarPostAdmin(client, MunicipioCorpo());
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();

        using HttpRequestMessage remover = new(
            HttpMethod.Delete, new Uri($"/api/configuracao/admin/base-legal-bonus-regional/{id}", UriKind.Relative));
        AutenticarComoAdmin(remover);

        HttpResponseMessage response = await client.SendAsync(remover);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage obter = await client.GetAsync(
            new Uri($"/api/configuracao/base-legal-bonus-regional/{id}", UriKind.Relative));
        obter.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "soft-delete tira o registro de circulação — o filtro global !IsDeleted esconde a linha");
    }

    private static object MunicipioCorpo(
        string identificacao = "Portaria Unifesspa nº 2514/2023",
        object[]? municipios = null) => new
        {
            tipoInstrumento = "PORTARIA",
            identificacao,
            descricao = "Institui inclusão regional",
            municipios = municipios ?? [new { codigoIbge = "1504208", nome = "Marabá", uf = "PA" }],
        };

    private static void AutenticarComoAdmin(HttpRequestMessage request)
    {
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
    }

    private static async Task<HttpResponseMessage> EnviarPostAdmin(HttpClient client, object body)
    {
        using HttpRequestMessage request = new(
            HttpMethod.Post, new Uri("/api/configuracao/admin/base-legal-bonus-regional", UriKind.Relative));
        AutenticarComoAdmin(request);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }
}
