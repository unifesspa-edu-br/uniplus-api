namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.LocaisOferta;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

/// <summary>
/// Smoke + caminho de escrita dos endpoints de <c>LocalOferta</c> (UNI-REQ #587).
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class LocalOfertaEndpointTests
{
    private readonly ConfiguracaoEndpointFixture _fixture;

    public LocalOfertaEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "GET /api/locais-oferta retorna 200 com Content-Type vendor MIME de local-oferta")]
    public async Task Listar_Retorna200ComVendorMime()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(new Uri("/api/locais-oferta", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType
            .Should().Be("application/vnd.uniplus.local-oferta.v1+json");
    }

    [Fact(DisplayName = "POST /api/admin/locais-oferta sem autenticação retorna 401")]
    public async Task Criar_SemAuth_Retorna401()
    {
        using HttpClient client = _fixture.Factory.CreateDefaultClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/admin/locais-oferta", UriKind.Relative));
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "POST /api/admin/locais-oferta cria (201) sem campus responsável e o GET retorna a cidade")]
    public async Task Criar_SemCampus_Retorna201()
    {
        var body = new
        {
            tipo = CamelCase(TipoLocalOferta.PoloEad),
            campusResponsavelId = (Guid?)null,
            cidadeCodigoIbge = "1504208",
            cidadeNome = "Marabá",
            cidadeUf = "PA",
        };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarPostAdmin(client, "/api/admin/locais-oferta", body);

        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();

        HttpResponseMessage obter = await client.GetAsync(new Uri($"/api/locais-oferta/{id}", UriKind.Relative));
        obter.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument doc = JsonDocument.Parse(await obter.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("tipo").GetString().Should().Be(CamelCase(TipoLocalOferta.PoloEad));
        doc.RootElement.GetProperty("cidadeCodigoIbge").GetString().Should().Be("1504208");
    }

    [Fact(DisplayName = "POST /api/admin/locais-oferta com campus responsável inexistente retorna 422")]
    public async Task Criar_CampusInexistente_Retorna422()
    {
        var body = new
        {
            tipo = CamelCase(TipoLocalOferta.CursoForaDeSede),
            campusResponsavelId = Guid.NewGuid(),
            cidadeCodigoIbge = "1504208",
            cidadeNome = "Marabá",
            cidadeUf = "PA",
        };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPostAdmin(client, "/api/admin/locais-oferta", body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    private static string CamelCase(TipoLocalOferta tipo) =>
        System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(tipo.ToString());

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
