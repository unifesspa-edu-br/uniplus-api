namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.RecursosAcessibilidade;

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.RecursosAcessibilidade.DadosDeRecursoAcessibilidade;

/// <summary>
/// Smoke + caminho de escrita dos endpoints de <c>RecursoAcessibilidade</c>
/// (UNI-REQ-0012): routing, vendor media type, HATEOAS, autenticação/autorização,
/// idempotência e unicidade do código e do nome (409) — com Wolverine contra Postgres efêmero.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class RecursoAcessibilidadeEndpointTests
{
    private readonly ConfiguracaoEndpointFixture _fixture;

    public RecursoAcessibilidadeEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "GET /api/configuracao/recursos-acessibilidade retorna 200 com Content-Type vendor MIME")]
    public async Task Listar_Retorna200ComVendorMime()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri("/api/configuracao/recursos-acessibilidade", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType
            .Should().Be("application/vnd.uniplus.recurso-acessibilidade.v1+json");
    }

    [Fact(DisplayName = "GET /api/configuracao/recursos-acessibilidade/{id} retorna 404 quando inexistente")]
    public async Task ObterPorId_NaoExiste_Retorna404()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri($"/api/configuracao/recursos-acessibilidade/{Guid.NewGuid()}", UriKind.Relative));

        await RespostaDeNaoEncontrado.ConferirAsync(response, "uniplus.configuracao.recurso_acessibilidade.nao_encontrado");
    }

    [Fact(DisplayName = "POST /api/configuracao/admin/recursos-acessibilidade sem autenticação retorna 401")]
    public async Task Criar_SemAuth_Retorna401()
    {
        using HttpClient client = _fixture.Factory.CreateDefaultClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/configuracao/admin/recursos-acessibilidade", UriKind.Relative));
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "POST sem Idempotency-Key retorna 400")]
    public async Task Criar_SemIdempotencyKey_Retorna400()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/configuracao/admin/recursos-acessibilidade", UriKind.Relative));
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
        string nome = NomeUnico();
        object body = DadosParaCriacao(codigo, nome);

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarPostAdmin(client, body);

        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();
        id.Should().NotBe(Guid.Empty);

        await ConferirRecursoObtido(client, id, codigo, nome);
    }

    [Fact(DisplayName = "POST com nome já existente entre vivos retorna 409")]
    public async Task Criar_NomeDuplicado_Retorna409()
    {
        string nome = NomeUnico();
        var body = new { codigo = CodigoUnico(), nome, descricao = "Primeiro" };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage primeiro = await EnviarPostAdmin(client, body);
        primeiro.StatusCode.Should().Be(HttpStatusCode.Created);

        HttpResponseMessage segundo = await EnviarPostAdmin(client, new { codigo = CodigoUnico(), nome, descricao = "Segundo" });
        segundo.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact(DisplayName = "POST com código já existente entre vivos retorna 409 com o vendor code do código")]
    public async Task Criar_CodigoDuplicado_Retorna409()
    {
        string codigo = CodigoUnico();

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage primeiro = await EnviarPostAdmin(client, new { codigo, nome = NomeUnico() });
        primeiro.StatusCode.Should().Be(HttpStatusCode.Created);

        HttpResponseMessage segundo = await EnviarPostAdmin(client, new { codigo, nome = NomeUnico() });
        segundo.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using JsonDocument problema = JsonDocument.Parse(await segundo.Content.ReadAsStringAsync());
        problema.RootElement.GetProperty("code").GetString()
            .Should().Be("uniplus.configuracao.recurso_acessibilidade.codigo_ja_existe");
    }

    private static object DadosParaCriacao(string codigo, string nome) => new
    {
        codigo,
        nome,
        descricao = "Profissional que lê a prova ao candidato",
    };

    private static async Task ConferirRecursoObtido(HttpClient client, Guid id, string codigo, string nome)
    {
        HttpResponseMessage resposta = await client.GetAsync(
            new Uri($"/api/configuracao/recursos-acessibilidade/{id}", UriKind.Relative));
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        JsonElement root = doc.RootElement;
        root.GetProperty("codigo").GetString().Should().Be(codigo);
        root.GetProperty("nome").GetString().Should().Be(nome);
        root.GetProperty("descricao").GetString().Should().Be("Profissional que lê a prova ao candidato");
        root.TryGetProperty("_links", out _).Should().BeTrue("HATEOAS Level 1 expõe _links.self (ADR-0029)");
    }

    private static async Task<HttpResponseMessage> EnviarPostAdmin(HttpClient client, object body)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/configuracao/admin/recursos-acessibilidade", UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }
}
