namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.CategoriasDocumento;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

/// <summary>
/// Smoke + caminho de escrita dos endpoints de <c>CategoriaDocumento</c>
/// (UNI-REQ-0013): routing, vendor media type, HATEOAS, autenticação/autorização,
/// idempotência, formato fechado do código (422), ordem de exibição, unicidade do
/// código entre vivas (409) e liberação do código pelo soft-delete — com Wolverine
/// contra Postgres efêmero.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class CategoriaDocumentoEndpointTests
{
    private readonly ConfiguracaoEndpointFixture _fixture;

    public CategoriaDocumentoEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "GET /api/configuracao/categorias-documento/{id} retorna 404 quando inexistente")]
    public async Task ObterPorId_NaoExiste_Retorna404()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri($"/api/configuracao/categorias-documento/{Guid.NewGuid()}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "POST /api/configuracao/admin/categorias-documento sem autenticação retorna 401")]
    public async Task Criar_SemAuth_Retorna401()
    {
        using HttpClient client = _fixture.Factory.CreateDefaultClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/configuracao/admin/categorias-documento", UriKind.Relative));
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "POST autenticado sem role plataforma-admin retorna 403")]
    public async Task Criar_SemRoleAdmin_Retorna403()
    {
        var body = new { codigo = CodigoUnico(), nome = "Sem permissão" };

        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/configuracao/admin/categorias-documento", UriKind.Relative));
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
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/configuracao/admin/categorias-documento", UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "POST cria (201) e o GET subsequente devolve os campos, a ordem e o vendor MIME")]
    public async Task Criar_ComAuthEIdempotency_Retorna201EPersiste()
    {
        string codigo = CodigoUnico();
        var body = new
        {
            codigo,
            nome = "Documento processual",
            descricao = "Instrui o processo administrativo",
            ordem = 30,
        };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarPostAdmin(client, body);

        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();
        id.Should().NotBe(Guid.Empty);

        HttpResponseMessage obter = await client.GetAsync(
            new Uri($"/api/configuracao/categorias-documento/{id}", UriKind.Relative));
        obter.StatusCode.Should().Be(HttpStatusCode.OK);
        obter.Content.Headers.ContentType?.MediaType
            .Should().Be("application/vnd.uniplus.categoria-documento.v1+json");

        using JsonDocument doc = JsonDocument.Parse(await obter.Content.ReadAsStringAsync());
        JsonElement root = doc.RootElement;
        root.GetProperty("codigo").GetString().Should().Be(codigo);
        root.GetProperty("nome").GetString().Should().Be("Documento processual");
        root.GetProperty("ordem").GetInt32().Should().Be(30);
        root.TryGetProperty("_links", out _).Should().BeTrue("HATEOAS Level 1 expõe _links.self (ADR-0029)");
    }

    [Fact(DisplayName = "POST sem ordem informada é aceito e grava ordem zero, em vez de 400 de model binding")]
    public async Task Criar_SemOrdem_GravaZero()
    {
        var body = new { codigo = CodigoUnico(), nome = "Sem ordem informada" };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarPostAdmin(client, body);
        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();

        HttpResponseMessage obter = await client.GetAsync(
            new Uri($"/api/configuracao/categorias-documento/{id}", UriKind.Relative));
        using JsonDocument doc = JsonDocument.Parse(await obter.Content.ReadAsStringAsync());

        doc.RootElement.GetProperty("ordem").GetInt32().Should().Be(0);
    }

    [Fact(DisplayName = "POST com código fora do formato fechado retorna 422")]
    public async Task Criar_CodigoFormatoInvalido_Retorna422()
    {
        var body = new { codigo = "01", nome = "Formato inválido" };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPostAdmin(client, body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement erro = doc.RootElement.GetProperty("errors").EnumerateArray().Single();
        erro.GetProperty("field").GetString().Should().Be("codigo");
        erro.GetProperty("code").GetString()
            .Should().Be("uniplus.configuracao.categoria_documento.codigo_formato_invalido",
                "o erro de forma é distinguível do de campo obrigatório e do de código já existente");
    }

    [Fact(DisplayName = "POST com ordem negativa retorna 422 no campo ordem")]
    public async Task Criar_OrdemNegativa_Retorna422()
    {
        var body = new { codigo = CodigoUnico(), nome = "Ordem negativa", ordem = -1 };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPostAdmin(client, body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("errors").EnumerateArray().Single()
            .GetProperty("field").GetString().Should().Be("ordem");
    }

    [Fact(DisplayName = "POST com código já existente entre vivas retorna 409")]
    public async Task Criar_CodigoDuplicado_Retorna409()
    {
        string codigo = CodigoUnico();

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage primeiro = await EnviarPostAdmin(client, new { codigo, nome = "Primeira" });
        primeiro.StatusCode.Should().Be(HttpStatusCode.Created);

        HttpResponseMessage segundo = await EnviarPostAdmin(client, new { codigo, nome = "Segunda" });
        segundo.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact(DisplayName = "PUT atualiza nome e ordem da categoria (204) e o GET reflete")]
    public async Task Atualizar_Valido_Retorna204ERefleteNoGet()
    {
        string codigo = CodigoUnico();

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarPostAdmin(client, new { codigo, nome = "Nome original", ordem = 10 });
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();

        using HttpRequestMessage request = new(
            HttpMethod.Put, new Uri($"/api/configuracao/admin/categorias-documento/{id}", UriKind.Relative));
        AutenticarComoAdmin(request);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(new { id, codigo, nome = "Nome revisado", ordem = 55 });

        HttpResponseMessage atualizar = await client.SendAsync(request);
        atualizar.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage obter = await client.GetAsync(
            new Uri($"/api/configuracao/categorias-documento/{id}", UriKind.Relative));
        using JsonDocument doc = JsonDocument.Parse(await obter.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("nome").GetString().Should().Be("Nome revisado");
        doc.RootElement.GetProperty("ordem").GetInt32().Should().Be(55);
    }

    [Fact(DisplayName = "DELETE remove a categoria (204) e o código volta a ficar disponível")]
    public async Task Remover_LiberaOCodigoParaNovoCadastro()
    {
        string codigo = CodigoUnico();

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarPostAdmin(client, new { codigo, nome = "Obsoleta" });
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();

        using HttpRequestMessage request = new(
            HttpMethod.Delete, new Uri($"/api/configuracao/admin/categorias-documento/{id}", UriKind.Relative));
        AutenticarComoAdmin(request);

        HttpResponseMessage remover = await client.SendAsync(request);
        remover.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage obterRemovida = await client.GetAsync(
            new Uri($"/api/configuracao/categorias-documento/{id}", UriKind.Relative));
        obterRemovida.StatusCode.Should().Be(HttpStatusCode.NotFound);

        HttpResponseMessage recriar = await EnviarPostAdmin(client, new { codigo, nome = "Recriada" });
        recriar.StatusCode.Should().Be(HttpStatusCode.Created,
            "o soft-delete libera o slot do índice único parcial");
    }

    [Fact(DisplayName = "DELETE de id inexistente retorna 404")]
    public async Task Remover_Inexistente_Retorna404()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(
            HttpMethod.Delete, new Uri($"/api/configuracao/admin/categorias-documento/{Guid.NewGuid()}", UriKind.Relative));
        AutenticarComoAdmin(request);

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "POST com código e nome inválidos ao mesmo tempo acumula as duas violações em errors[]")]
    public async Task Criar_CodigoENomeInvalidos_AcumulaAsDuasViolacoesEmErrors()
    {
        var body = new { codigo = "invalido-minusculo", nome = "" };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPostAdmin(client, body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement erros = doc.RootElement.GetProperty("errors");
        erros.GetArrayLength().Should().Be(2);
        erros.EnumerateArray().Select(e => e.GetProperty("field").GetString())
            .Should().BeEquivalentTo(["codigo", "nome"]);
    }

    [Fact(DisplayName = "POST com código genuinamente ausente no JSON retorna 422 (não 400 de model binding)")]
    public async Task Criar_CodigoAusenteNoJson_Retorna422()
    {
        var body = new { nome = "Nome válido" };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPostAdmin(client, body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "sem validator FluentValidation a montante, o campo ausente precisa chegar ao domínio (ADR-0125), não virar 400 de model binding");
    }

    private static string CodigoUnico() => $"CAT_{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}";

    private static void AutenticarComoAdmin(HttpRequestMessage request)
    {
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
    }

    private static async Task<HttpResponseMessage> EnviarPostAdmin(HttpClient client, object body)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/configuracao/admin/categorias-documento", UriKind.Relative));
        AutenticarComoAdmin(request);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }
}
