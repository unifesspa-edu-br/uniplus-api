namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Cursos;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

/// <summary>
/// Smoke + caminho de escrita dos endpoints de <c>Curso</c> (story #588):
/// routing, vendor media type, HATEOAS, autenticação/autorização, idempotência,
/// domínio fechado do grupo de área do ENEM (422), unicidade do código (409),
/// ciclo CRUD completo e soft-delete — com Wolverine contra Postgres efêmero.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class CursoEndpointTests
{
    private readonly ConfiguracaoEndpointFixture _fixture;

    public CursoEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "GET /api/configuracao/cursos retorna 200 com Content-Type vendor MIME")]
    public async Task Listar_Retorna200ComVendorMime()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri("/api/configuracao/cursos", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType
            .Should().Be("application/vnd.uniplus.curso.v1+json");
    }

    [Fact(DisplayName = "GET /api/configuracao/cursos/{id} retorna 404 quando inexistente")]
    public async Task ObterPorId_NaoExiste_Retorna404()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri($"/api/configuracao/cursos/{Guid.NewGuid()}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "POST /api/configuracao/admin/cursos sem autenticação retorna 401")]
    public async Task Criar_SemAuth_Retorna401()
    {
        using HttpClient client = _fixture.Factory.CreateDefaultClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/configuracao/admin/cursos", UriKind.Relative));
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "POST autenticado sem role plataforma-admin retorna 403")]
    public async Task Criar_SemRoleAdmin_Retorna403()
    {
        var body = new { codigo = CodigoUnico(), nome = "Sem permissão", grau = "Bacharelado", nivelEnsino = "Graduação" };

        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/configuracao/admin/cursos", UriKind.Relative));
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
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/configuracao/admin/cursos", UriKind.Relative));
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
            nome = "Engenharia Civil",
            grau = "Bacharelado",
            nivelEnsino = "Graduação",
            grupoAreaEnem = "Tecnológica",
        };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarPostAdmin(client, body);

        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();
        id.Should().NotBe(Guid.Empty);

        HttpResponseMessage obter = await client.GetAsync(
            new Uri($"/api/configuracao/cursos/{id}", UriKind.Relative));
        obter.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument doc = JsonDocument.Parse(await obter.Content.ReadAsStringAsync());
        JsonElement root = doc.RootElement;
        root.GetProperty("codigo").GetString().Should().Be(codigo);
        root.GetProperty("nome").GetString().Should().Be("Engenharia Civil");
        root.GetProperty("grau").GetString().Should().Be("Bacharelado");
        root.GetProperty("nivelEnsino").GetString().Should().Be("Graduação");
        root.GetProperty("grupoAreaEnem").GetString().Should().Be("Tecnológica");
        root.TryGetProperty("_links", out _).Should().BeTrue("HATEOAS Level 1 expõe _links.self (ADR-0029)");
    }

    [Fact(DisplayName = "POST sem grupo de área do ENEM cria (201) e o GET retorna grupoAreaEnem nulo")]
    public async Task Criar_SemGrupoAreaEnem_Retorna201ComGrupoNulo()
    {
        var body = new { codigo = CodigoUnico(), nome = "Direito", grau = "Bacharelado", nivelEnsino = "Graduação" };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarPostAdmin(client, body);
        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();

        HttpResponseMessage obter = await client.GetAsync(
            new Uri($"/api/configuracao/cursos/{id}", UriKind.Relative));
        using JsonDocument doc = JsonDocument.Parse(await obter.Content.ReadAsStringAsync());

        doc.RootElement.GetProperty("grupoAreaEnem").ValueKind.Should().Be(JsonValueKind.Null,
            "nem todo curso classifica por área do ENEM");
    }

    [Fact(DisplayName = "Contrato de matriz curricular pura: o recurso não expõe código e-MEC, local nem unidade")]
    public async Task Criar_RecursoNaoExpoeCamposDeOferta()
    {
        var body = new { codigo = CodigoUnico(), nome = "Pedagogia", grau = "Licenciatura", nivelEnsino = "Graduação" };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarPostAdmin(client, body);
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();

        HttpResponseMessage obter = await client.GetAsync(
            new Uri($"/api/configuracao/cursos/{id}", UriKind.Relative));
        using JsonDocument doc = JsonDocument.Parse(await obter.Content.ReadAsStringAsync());

        // A separação central da #748: o curso é matriz curricular pura; código
        // e-MEC, local de oferta e unidade pertencem à OfertaCurso (#749).
        string[] propriedadesDeOfertaProibidas =
            ["codigoEmec", "localOfertaId", "unidadeId", "campusId", "turno", "vagas"];
        foreach (string proibida in propriedadesDeOfertaProibidas)
        {
            doc.RootElement.TryGetProperty(proibida, out _)
                .Should().BeFalse($"o curso é matriz curricular pura e não deve expor '{proibida}'");
        }
    }

    [Fact(DisplayName = "POST com grupo de área do ENEM fora do domínio fechado retorna 422")]
    public async Task Criar_GrupoAreaEnemInvalido_Retorna422()
    {
        var body = new
        {
            codigo = CodigoUnico(),
            nome = "Grupo inválido",
            grau = "Bacharelado",
            nivelEnsino = "Graduação",
            grupoAreaEnem = "Exatas",
        };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPostAdmin(client, body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "POST com código já existente entre vivos retorna 409")]
    public async Task Criar_CodigoDuplicado_Retorna409()
    {
        string codigo = CodigoUnico();
        var body = new { codigo, nome = "Primeiro", grau = "Bacharelado", nivelEnsino = "Graduação" };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage primeiro = await EnviarPostAdmin(client, body);
        primeiro.StatusCode.Should().Be(HttpStatusCode.Created);

        HttpResponseMessage segundo = await EnviarPostAdmin(
            client, new { codigo, nome = "Segundo", grau = "Bacharelado", nivelEnsino = "Graduação" });
        segundo.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact(DisplayName = "Ciclo CRUD completo: POST → PUT (204) → GET reflete edição → DELETE (204) → GET 404 (soft-delete)")]
    public async Task CicloCrudCompleto_CriaEditaRemove()
    {
        string codigo = CodigoUnico();
        var body = new { codigo, nome = "Engenharia Civil", grau = "Bacharelado", nivelEnsino = "Graduação" };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarPostAdmin(client, body);
        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();

        // PUT: código é editável; grupo de área do ENEM passa a Tecnológica.
        string codigoNovo = CodigoUnico();
        var bodyPut = new
        {
            id,
            codigo = codigoNovo,
            nome = "Engenharia Civil Integral",
            grau = "Licenciatura",
            nivelEnsino = "Mestrado",
            grupoAreaEnem = "Tecnológica",
        };
        HttpResponseMessage atualizar = await EnviarPutAdmin(client, id, bodyPut);
        atualizar.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage obter = await client.GetAsync(
            new Uri($"/api/configuracao/cursos/{id}", UriKind.Relative));
        obter.StatusCode.Should().Be(HttpStatusCode.OK);
        using (JsonDocument doc = JsonDocument.Parse(await obter.Content.ReadAsStringAsync()))
        {
            JsonElement root = doc.RootElement;
            root.GetProperty("codigo").GetString().Should().Be(codigoNovo);
            root.GetProperty("nome").GetString().Should().Be("Engenharia Civil Integral");
            root.GetProperty("grau").GetString().Should().Be("Licenciatura");
            root.GetProperty("nivelEnsino").GetString().Should().Be("Mestrado");
            root.GetProperty("grupoAreaEnem").GetString().Should().Be("Tecnológica");
        }

        // DELETE: soft-delete — sem oferta de curso viva (#749), a remoção não bloqueia.
        HttpResponseMessage remover = await EnviarDeleteAdmin(client, id);
        remover.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage aposRemocao = await client.GetAsync(
            new Uri($"/api/configuracao/cursos/{id}", UriKind.Relative));
        aposRemocao.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "o soft-delete tira o curso das leituras via query filter global");
    }

    private static string CodigoUnico() => $"CUR_{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}";

    private static async Task<HttpResponseMessage> EnviarPostAdmin(HttpClient client, object body)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/configuracao/admin/cursos", UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> EnviarPutAdmin(HttpClient client, Guid id, object body)
    {
        using HttpRequestMessage request = new(HttpMethod.Put, new Uri($"/api/configuracao/admin/cursos/{id}", UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> EnviarDeleteAdmin(HttpClient client, Guid id)
    {
        using HttpRequestMessage request = new(HttpMethod.Delete, new Uri($"/api/configuracao/admin/cursos/{id}", UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
        return await client.SendAsync(request);
    }
}
