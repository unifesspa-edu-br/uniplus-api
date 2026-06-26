namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.PesosAreaEnem;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

/// <summary>
/// Smoke + caminho de escrita dos endpoints de <c>PesoAreaEnem</c> (UNI-REQ-0066):
/// routing, vendor media type, HATEOAS, autenticação/autorização, idempotência,
/// domínio fechado do grupo (CA-03) e corte padrão 400 (CA-04) com Wolverine
/// rodando contra Postgres efêmero.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class PesoAreaEnemEndpointTests
{
    private const string VendorMime = "application/vnd.uniplus.peso-area-enem.v1+json";
    private const string ColecaoPath = "/api/configuracao/pesos-area-enem";
    private const string AdminPath = "/api/configuracao/admin/pesos-area-enem";
    private const string BaseLegal = "Res. 805/2024 Anexo I";

    private readonly ConfiguracaoEndpointFixture _fixture;

    public PesoAreaEnemEndpointTests(ConfiguracaoEndpointFixture fixture)
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
        request.Content = JsonContent.Create(CorpoValido(ResolucaoUnica()));

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "POST admin cria (201) e o GET subsequente retorna os pesos + _links")]
    public async Task Criar_ComAuthEIdempotency_Retorna201EPersiste()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarPostAdmin(client, AdminPath, CorpoValido(ResolucaoUnica()));

        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();
        id.Should().NotBe(Guid.Empty);

        HttpResponseMessage obter = await client.GetAsync(new Uri($"{ColecaoPath}/{id}", UriKind.Relative));
        obter.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument doc = JsonDocument.Parse(await obter.Content.ReadAsStringAsync());
        JsonElement root = doc.RootElement;
        root.GetProperty("grupoCurso").GetString().Should().Be(GrupoCurso.Tecnologica);
        root.GetProperty("pesoRedacao").GetDecimal().Should().Be(1.50m);
        root.GetProperty("corteRedacao").GetDecimal().Should().Be(400m);
        root.GetProperty("baseLegal").GetString().Should().Be(BaseLegal);
        root.TryGetProperty("_links", out _).Should().BeTrue("HATEOAS Level 1 expõe _links.self (ADR-0029)");
    }

    [Fact(DisplayName = "CA-04: POST admin sem corte de redação assume o padrão 400")]
    public async Task Criar_SemCorte_AssumePadrao400()
    {
        var body = new
        {
            resolucao = ResolucaoUnica(),
            grupoCurso = GrupoCurso.Tecnologica,
            pesoRedacao = 1.50m,
            pesoCienciasNatureza = 1.00m,
            pesoCienciasHumanas = 1.00m,
            pesoLinguagens = 1.00m,
            pesoMatematica = 2.00m,
            baseLegal = BaseLegal,
        };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarPostAdmin(client, AdminPath, body);
        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();

        HttpResponseMessage obter = await client.GetAsync(new Uri($"{ColecaoPath}/{id}", UriKind.Relative));
        using JsonDocument doc = JsonDocument.Parse(await obter.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("corteRedacao").GetDecimal().Should().Be(400m);
    }

    [Fact(DisplayName = "CA-03: POST admin com grupo fora do domínio retorna 422")]
    public async Task Criar_GrupoInvalido_Retorna422()
    {
        var body = new
        {
            resolucao = ResolucaoUnica(),
            grupoCurso = "Engenharias",
            pesoRedacao = 1.50m,
            pesoCienciasNatureza = 1.00m,
            pesoCienciasHumanas = 1.00m,
            pesoLinguagens = 1.00m,
            pesoMatematica = 2.00m,
            corteRedacao = 400m,
            baseLegal = BaseLegal,
        };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPostAdmin(client, AdminPath, body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    private static object CorpoValido(string resolucao) => new
    {
        resolucao,
        grupoCurso = GrupoCurso.Tecnologica,
        pesoRedacao = 1.50m,
        pesoCienciasNatureza = 1.00m,
        pesoCienciasHumanas = 1.00m,
        pesoLinguagens = 1.00m,
        pesoMatematica = 2.00m,
        corteRedacao = 400m,
        baseLegal = BaseLegal,
    };

    // Resolução de até 40 chars, única por teste para não colidir na UNIQUE parcial do par.
    private static string ResolucaoUnica() => $"Res. {Guid.NewGuid().ToString("N")[..12]}";

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
