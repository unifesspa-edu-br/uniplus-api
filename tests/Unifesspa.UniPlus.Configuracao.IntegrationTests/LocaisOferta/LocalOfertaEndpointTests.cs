namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.LocaisOferta;

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

/// <summary>
/// Smoke + caminho de escrita dos endpoints de <c>LocalOferta</c> (UNI-REQ #587).
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class LocalOfertaEndpointTests
{
    private const string RotaAdmin = "/api/configuracao/admin/locais-oferta";

    private readonly ConfiguracaoEndpointFixture _fixture;

    public LocalOfertaEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "GET /api/configuracao/locais-oferta retorna 200 com Content-Type vendor MIME de local-oferta")]
    public async Task Listar_Retorna200ComVendorMime()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(new Uri("/api/configuracao/locais-oferta", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType
            .Should().Be("application/vnd.uniplus.local-oferta.v1+json");
    }

    [Fact(DisplayName = "POST /api/configuracao/admin/locais-oferta sem autenticação retorna 401")]
    public async Task Criar_SemAuth_Retorna401()
    {
        using HttpClient client = _fixture.Factory.CreateDefaultClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/configuracao/admin/locais-oferta", UriKind.Relative));
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "POST /api/configuracao/admin/locais-oferta cria (201) sem campus responsável e o GET retorna o tipo com código e rótulo e a cidade")]
    public async Task Criar_SemCampus_Retorna201()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarPostAdmin(client, RotaAdmin, CorpoDoLocal("POLO_DE_EDUCACAO_A_DISTANCIA", null));

        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        using JsonDocument doc = await LerLocalAsync(client, await criar.Content.ReadFromJsonAsync<Guid>());

        JsonElement tipo = doc.RootElement.GetProperty("tipo");
        tipo.GetProperty("codigo").GetString().Should().Be("POLO_DE_EDUCACAO_A_DISTANCIA");
        tipo.GetProperty("rotulo").GetString().Should().Be("Polo de educação a distância");
        doc.RootElement.GetProperty("cidade").GetProperty("codigoIbge").GetString().Should().Be("1504208");
    }

    [Fact(DisplayName = "POST /api/configuracao/admin/locais-oferta com campus responsável existente cria (201) — FK satisfeita")]
    public async Task Criar_ComCampusExistente_Retorna201()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid campusId = await CriarCampusAsync(client);

        HttpResponseMessage criarLocal = await EnviarPostAdmin(client, RotaAdmin, CorpoDoLocal("CURSO_FORA_DE_SEDE", campusId));

        criarLocal.StatusCode.Should().Be(HttpStatusCode.Created);
        using JsonDocument doc = await LerLocalAsync(client, await criarLocal.Content.ReadFromJsonAsync<Guid>());
        doc.RootElement.GetProperty("campusResponsavelId").GetGuid().Should().Be(campusId);
    }

    [Fact(DisplayName = "POST /api/configuracao/admin/locais-oferta com campus responsável inexistente retorna 422")]
    public async Task Criar_CampusInexistente_Retorna422()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPostAdmin(client, RotaAdmin, CorpoDoLocal("CURSO_FORA_DE_SEDE", Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "POST com tipo ausente e cidade ausente ao mesmo tempo acumula as violações em errors[]")]
    public async Task Criar_TipoAusenteECidadeAusente_AcumulaAsViolacoesEmErrors()
    {
        var body = new { campusResponsavelId = (Guid?)null };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPostAdmin(client, "/api/configuracao/admin/locais-oferta", body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "tipo e cidade ausentes precisam chegar ao domínio (ADR-0125), nenhum dos dois deve virar 400 de model binding");
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement erros = doc.RootElement.GetProperty("errors");
        erros.GetArrayLength().Should().Be(4);
        erros.EnumerateArray().Select(e => e.GetProperty("field").GetString())
            .Should().BeEquivalentTo(["tipo", "cidadeCodigoIbge", "cidadeNome", "cidadeUf"]);
    }

    [Fact(DisplayName = "POST com tipo fora do contrato retorna 422 no campo tipo, sem repetir o valor recebido")]
    public async Task Criar_TipoForaDoContrato_Retorna422NoCampoTipo()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPostAdmin(client, RotaAdmin, CorpoDoLocal("INEXISTENTE", null));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        string conteudo = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(conteudo);
        JsonElement erro = doc.RootElement.GetProperty("errors")[0];
        erro.GetProperty("field").GetString().Should().Be("tipo");
        erro.GetProperty("code").GetString().Should().Be("uniplus.configuracao.local_oferta.tipo_invalido");
        conteudo.Should().NotContain("INEXISTENTE");
    }

    private static object CorpoDoLocal(string tipo, Guid? campusResponsavelId) =>
        new
        {
            tipo,
            campusResponsavelId,
            cidadeCodigoIbge = "1504208",
            cidadeNome = "Marabá",
            cidadeUf = "PA",
        };

    private static async Task<Guid> CriarCampusAsync(HttpClient client)
    {
        var campusBody = new
        {
            sigla = $"C{Guid.NewGuid().ToString("N")[..6]}",
            nome = "Campus Responsável",
            cidadeCodigoIbge = "1504208",
            cidadeNome = "Marabá",
            cidadeUf = "PA",
        };
        HttpResponseMessage criarCampus = await EnviarPostAdmin(client, "/api/configuracao/admin/campi", campusBody);
        criarCampus.StatusCode.Should().Be(HttpStatusCode.Created);

        return await criarCampus.Content.ReadFromJsonAsync<Guid>();
    }

    private static async Task<JsonDocument> LerLocalAsync(HttpClient client, Guid id)
    {
        HttpResponseMessage obter = await client.GetAsync(new Uri($"/api/configuracao/locais-oferta/{id}", UriKind.Relative));
        obter.StatusCode.Should().Be(HttpStatusCode.OK);

        return JsonDocument.Parse(await obter.Content.ReadAsStringAsync());
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
