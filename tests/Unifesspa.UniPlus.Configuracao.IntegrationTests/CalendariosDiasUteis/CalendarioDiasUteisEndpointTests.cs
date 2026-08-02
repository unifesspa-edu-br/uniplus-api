namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.CalendariosDiasUteis;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

/// <summary>
/// Smoke + caminho de escrita dos endpoints de <c>CalendarioDiasUteis</c>
/// (UNI-REQ-0080): routing, vendor media type, HATEOAS, autenticação/autorização,
/// idempotência, invariante de vigência única e bloqueio de remoção do dataset
/// vigente, com Wolverine rodando contra Postgres efêmero.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class CalendarioDiasUteisEndpointTests
{
    private const string VendorMime = "application/vnd.uniplus.calendario-dias-uteis.v1+json";
    private const string ColecaoPath = "/api/configuracao/calendarios-dias-uteis";
    private const string AdminPath = "/api/configuracao/admin/calendarios-dias-uteis";

    private readonly ConfiguracaoEndpointFixture _fixture;

    public CalendarioDiasUteisEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "GET coleção retorna 200 com Content-Type vendor MIME")]
    public async Task Listar_Retorna200ComVendorMime()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(new Uri(ColecaoPath, UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be(VendorMime);
    }

    [Fact(DisplayName = "GET por id retorna 404 quando inexistente")]
    public async Task ObterPorId_NaoExiste_Retorna404()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri($"{ColecaoPath}/{Guid.NewGuid()}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "POST admin sem autenticação retorna 401")]
    public async Task Criar_SemAuth_Retorna401()
    {
        using HttpClient client = _fixture.Factory.CreateDefaultClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri(AdminPath, UriKind.Relative));
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "POST admin autenticado sem role plataforma-admin retorna 403")]
    public async Task Criar_SemRoleAdmin_Retorna403()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri(AdminPath, UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "candidato");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(CorpoValido());

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "POST admin cria (201 + Location) e o GET subsequente retorna os dias não úteis + _links")]
    public async Task Criar_ComAuthEIdempotency_Retorna201EPersiste()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarPostAdmin(client, AdminPath, CorpoValido(VersaoUnica()));

        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        criar.Headers.Location.Should().NotBeNull();
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();
        id.Should().NotBe(Guid.Empty);

        HttpResponseMessage obter = await client.GetAsync(new Uri($"{ColecaoPath}/{id}", UriKind.Relative));
        obter.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument doc = JsonDocument.Parse(await obter.Content.ReadAsStringAsync());
        JsonElement root = doc.RootElement;
        root.GetProperty("vigente").GetBoolean().Should().BeFalse("todo dataset nasce não vigente");
        JsonElement dias = root.GetProperty("diasNaoUteis");
        dias.GetArrayLength().Should().Be(2);
        dias.EnumerateArray().Should().Contain(d => d.GetProperty("abrangencia").GetString() == "NACIONAL");
        dias.EnumerateArray().Should().Contain(d =>
            d.GetProperty("abrangencia").GetString() == "MUNICIPAL"
            && d.GetProperty("municipioIbge").GetString() == "1501402");
        root.TryGetProperty("_links", out _).Should().BeTrue("HATEOAS Level 1 expõe _links.self (ADR-0029)");
    }

    [Fact(DisplayName = "GET coleção não inclui diasNaoUteis — a listagem projeta só o resumo")]
    public async Task Listar_NaoIncluiDiasNaoUteis()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarPostAdmin(client, AdminPath, CorpoValido(VersaoUnica()));
        criar.StatusCode.Should().Be(HttpStatusCode.Created);

        HttpResponseMessage listar = await client.GetAsync(new Uri(ColecaoPath, UriKind.Relative));

        listar.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument doc = JsonDocument.Parse(await listar.Content.ReadAsStringAsync());
        JsonElement itens = doc.RootElement;
        itens.GetArrayLength().Should().BeGreaterThan(0);
        foreach (JsonElement item in itens.EnumerateArray())
        {
            item.TryGetProperty("diasNaoUteis", out _).Should().BeFalse(
                "a listagem não carrega a coleção filha — use ObterPorId para o dataset completo");
        }
    }

    [Fact(DisplayName = "POST .../{id}/vigente marca o novo vigente e desmarca o vigente anterior")]
    public async Task MarcarVigente_DesmarcaOAnterior()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        Guid primeiroId = await CriarDataset(client, VersaoUnica());
        HttpResponseMessage marcarPrimeiro = await EnviarPostAdmin(
            client, $"{AdminPath}/{primeiroId}/vigente", null);
        marcarPrimeiro.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await ObterVigente(client, primeiroId)).Should().BeTrue();

        Guid segundoId = await CriarDataset(client, VersaoUnica());
        HttpResponseMessage marcarSegundo = await EnviarPostAdmin(
            client, $"{AdminPath}/{segundoId}/vigente", null);
        marcarSegundo.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await ObterVigente(client, segundoId)).Should().BeTrue();
        (await ObterVigente(client, primeiroId)).Should().BeFalse("no máximo um dataset é vigente por vez");
    }

    [Fact(DisplayName = "DELETE em dataset vigente retorna 409 com o código de erro NaoRemoveVigente")]
    public async Task Remover_DatasetVigente_Retorna409()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CriarDataset(client, VersaoUnica());
        HttpResponseMessage marcarVigente = await EnviarPostAdmin(client, $"{AdminPath}/{id}/vigente", null);
        marcarVigente.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage response = await EnviarDeleteAdmin(client, id);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await LerCodigoDeErro(response)).Should().Be(
            "uniplus.configuracao.calendario_dias_uteis.nao_remove_vigente");
    }

    [Fact(DisplayName = "DELETE em dataset não vigente retorna 204")]
    public async Task Remover_DatasetNaoVigente_Retorna204()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CriarDataset(client, VersaoUnica());

        HttpResponseMessage response = await EnviarDeleteAdmin(client, id);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private static object CorpoValido(string? versaoDataset = null) => new
    {
        versaoDataset = versaoDataset ?? "2027.1",
        diasNaoUteis = new object[]
        {
            new { abrangencia = "NACIONAL", municipioIbge = (string?)null, data = "2027-01-01", descricao = "Confraternização Universal" },
            new { abrangencia = "MUNICIPAL", municipioIbge = "1501402", data = "2027-05-08", descricao = "Aniversário de Marabá" },
        },
    };

    // Versão de até 60 chars, única por teste — não é chave natural, mas a
    // unicidade evita confundir datasets entre testes que rodam na mesma coleção.
    private static string VersaoUnica() => $"cal-{Guid.NewGuid():N}"[..20];

    private static async Task<Guid> CriarDataset(HttpClient client, string versaoDataset)
    {
        HttpResponseMessage criar = await EnviarPostAdmin(client, AdminPath, CorpoValido(versaoDataset));
        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        return await criar.Content.ReadFromJsonAsync<Guid>();
    }

    private static async Task<bool> ObterVigente(HttpClient client, Guid id)
    {
        HttpResponseMessage obter = await client.GetAsync(new Uri($"{ColecaoPath}/{id}", UriKind.Relative));
        using JsonDocument doc = JsonDocument.Parse(await obter.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("vigente").GetBoolean();
    }

    private static async Task<string?> LerCodigoDeErro(HttpResponseMessage response)
    {
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("code").GetString();
    }

    private static async Task<HttpResponseMessage> EnviarPostAdmin(HttpClient client, string path, object? body)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri(path, UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> EnviarDeleteAdmin(HttpClient client, Guid id)
    {
        using HttpRequestMessage request = new(
            HttpMethod.Delete, new Uri($"{AdminPath}/{id}", UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
        return await client.SendAsync(request);
    }
}
