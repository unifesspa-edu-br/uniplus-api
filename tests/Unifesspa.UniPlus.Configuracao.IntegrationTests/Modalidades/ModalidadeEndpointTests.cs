namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Modalidades;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;
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

    [Fact(DisplayName = "POST com código do catálogo legal fixo retorna 409 com o código de erro da reserva")]
    public async Task Criar_CodigoLegalFixo_Retorna409Reservado()
    {
        var body = new { codigo = "LB_PPI", naturezaLegal = "AMPLA" };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPostAdmin(client, body);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await LerCodigoDeErro(response)).Should().Be(
            "uniplus.configuracao.modalidade.criacao_bloqueada_codigo_protegido",
            "a reserva do código é resposta distinta de 'já existe uma modalidade com este código'");
    }

    [Fact(DisplayName = "DELETE de modalidade legal fixa retorna 409 e a modalidade continua viva")]
    public async Task Remover_LegalFixa_Retorna409EPermanece()
    {
        Guid id = IdSemeado("LB_PPI");

        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(
            HttpMethod.Delete, new Uri($"/api/configuracao/admin/modalidades/{id}", UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await LerCodigoDeErro(response)).Should().Be(
            "uniplus.configuracao.modalidade.remocao_bloqueada_codigo_protegido");

        HttpResponseMessage obter = await client.GetAsync(
            new Uri($"/api/configuracao/modalidades/{id}", UriKind.Relative));
        obter.StatusCode.Should().Be(HttpStatusCode.OK, "a cota federal continua no catálogo");
    }

    [Fact(DisplayName = "PUT que altera a estrutura de modalidade legal fixa retorna 422")]
    public async Task Atualizar_LegalFixaEstrutura_Retorna422()
    {
        Guid id = IdSemeado("LI_Q");

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPutAdmin(client, id, new
        {
            id,
            naturezaLegal = "AMPLA",
            composicaoVagas = "RESIDUAL_DO_VO",
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await LerCodigoDeErro(response)).Should().Be(
            "uniplus.configuracao.modalidade.estrutura_protegida_nao_editavel");
    }

    [Fact(DisplayName = "PUT que altera só descrição e base legal de modalidade legal fixa retorna 204 e persiste")]
    public async Task Atualizar_LegalFixaRedacao_Retorna204EPersiste()
    {
        Guid id = IdSemeado("LI_EP");
        string descricao = $"Cota — independente de renda, egresso de escola pública ({Guid.NewGuid():N})";
        const string BaseLegal = "Lei 12.711/2012, art. 3º (red. Lei 14.723/2023)";

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPutAdmin(client, id, new
        {
            id,
            descricao,
            naturezaLegal = "COTA_RESERVADA",
            composicaoVagas = "DENTRO_DO_VR",
            regraRemanejamento = "SEGUE_CASCATA",
            baseLegal = BaseLegal,
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage obter = await client.GetAsync(
            new Uri($"/api/configuracao/modalidades/{id}", UriKind.Relative));
        using JsonDocument doc = JsonDocument.Parse(await obter.Content.ReadAsStringAsync());
        JsonElement root = doc.RootElement;
        root.GetProperty("descricao").GetString().Should().Be(descricao);
        root.GetProperty("baseLegal").GetString().Should().Be(BaseLegal);
        root.GetProperty("naturezaLegal").GetString().Should().Be("COTA_RESERVADA",
            "a estrutura de vagas permanece a da lei");
        root.GetProperty("composicaoVagas").GetString().Should().Be("DENTRO_DO_VR");
        root.GetProperty("regraRemanejamento").GetString().Should().Be("SEGUE_CASCATA");
    }

    private static Guid IdSemeado(string codigo) =>
        ModalidadeSeed.Itens.Single(i => i.Codigo == codigo).Id;

    private static async Task<string?> LerCodigoDeErro(HttpResponseMessage response)
    {
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("code").GetString();
    }

    private static async Task<HttpResponseMessage> EnviarPutAdmin(HttpClient client, Guid id, object body)
    {
        using HttpRequestMessage request = new(
            HttpMethod.Put, new Uri($"/api/configuracao/admin/modalidades/{id}", UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
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
