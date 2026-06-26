namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.ReferenciasReservaDemografica;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

/// <summary>
/// Smoke + caminho de escrita dos endpoints de <c>ReferenciaReservaDemografica</c>
/// (UNI-REQ-0065): routing, vendor media type, HATEOAS, autenticação/autorização,
/// idempotência e validação de percentual (CA-03) com Wolverine rodando contra
/// Postgres efêmero.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class ReferenciaReservaDemograficaEndpointTests
{
    private const string VendorMime = "application/vnd.uniplus.referencia-reserva-demografica.v1+json";
    private const string ColecaoPath = "/api/configuracao/referencias-reserva-demografica";
    private const string AdminPath = "/api/configuracao/admin/referencias-reserva-demografica";

    private readonly ConfiguracaoEndpointFixture _fixture;

    public ReferenciaReservaDemograficaEndpointTests(ConfiguracaoEndpointFixture fixture)
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

    [Fact(DisplayName = "POST admin cria (201) e o GET subsequente retorna os percentuais + _links")]
    public async Task Criar_ComAuthEIdempotency_Retorna201EPersiste()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarPostAdmin(client, AdminPath, CorpoValido(CensoUnico()));

        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();
        id.Should().NotBe(Guid.Empty);

        HttpResponseMessage obter = await client.GetAsync(new Uri($"{ColecaoPath}/{id}", UriKind.Relative));
        obter.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument doc = JsonDocument.Parse(await obter.Content.ReadAsStringAsync());
        JsonElement root = doc.RootElement;
        root.GetProperty("ppiPercentual").GetDecimal().Should().Be(78.50m);
        root.GetProperty("baseLegal").GetString().Should().Be("Lei 12.711/2012, art. 10, III");
        root.TryGetProperty("_links", out _).Should().BeTrue("HATEOAS Level 1 expõe _links.self (ADR-0029)");
    }

    [Fact(DisplayName = "CA-03: POST admin com percentual fora do intervalo retorna 422")]
    public async Task Criar_PercentualInvalido_Retorna422()
    {
        var body = new
        {
            censoReferencia = CensoUnico(),
            ppiPercentual = 120.0m,
            quilombolaPercentual = 1.2m,
            pcdPercentual = 8.4m,
            baseLegal = "Lei 12.711/2012, art. 10, III",
        };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPostAdmin(client, AdminPath, body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    private static object CorpoValido(string? censo = null) => new
    {
        censoReferencia = censo ?? "2022",
        ppiPercentual = 78.50m,
        quilombolaPercentual = 1.20m,
        pcdPercentual = 8.40m,
        baseLegal = "Lei 12.711/2012, art. 10, III",
    };

    // Censo de até 20 chars, único por teste para não colidir na UNIQUE parcial entre casos.
    private static string CensoUnico() => Guid.NewGuid().ToString("N")[..12];

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
