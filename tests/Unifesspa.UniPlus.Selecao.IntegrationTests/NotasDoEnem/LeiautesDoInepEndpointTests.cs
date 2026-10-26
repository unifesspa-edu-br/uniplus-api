namespace Unifesspa.UniPlus.Selecao.IntegrationTests.NotasDoEnem;

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

using AwesomeAssertions;

using Outbox.Cascading;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

/// <summary>
/// O catálogo de leiautes do INEP na API: restrito a quem configura o processo, filtrável por tipo, e
/// só com os leiautes ativos.
/// </summary>
[Collection(CascadingCollection.Name)]
public sealed class LeiautesDoInepEndpointTests
{
    private const string Rota = "/api/selecao/leiautes-de-arquivo-do-inep";

    private readonly CascadingFixture _fixture;

    public LeiautesDoInepEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    private static HttpRequestMessage Requisicao(string url, string? papel)
    {
        HttpRequestMessage request = new(HttpMethod.Get, new Uri(url, UriKind.Relative));
        if (papel is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(
                TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
            request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, papel);
        }

        return request;
    }

    [Fact(DisplayName = "sem token é 401 e sem plataforma-admin é 403")]
    public async Task Listar_SemAutorizacao_Recusa()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpRequestMessage semToken = Requisicao(Rota, papel: null);
        using HttpRequestMessage semPapel = Requisicao(Rota, papel: "candidato");

        (await client.SendAsync(semToken)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.SendAsync(semPapel)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory(DisplayName = "o filtro por tipo devolve só os leiautes daquele tipo; sem filtro, os dois")]
    [InlineData("?tipo=Retorno", "INEP_ENEM_RETORNO", 20)]
    [InlineData("?tipo=Envio", "INEP_ENEM_ENVIO", 1)]
    public async Task Listar_FiltraPorTipo(string filtro, string codigo, int campos)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = Requisicao(Rota + filtro, "plataforma-admin");

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement leiaute = doc.RootElement.EnumerateArray().Should().ContainSingle().Subject;
        leiaute.GetProperty("codigo").GetString().Should().Be(codigo);
        leiaute.GetProperty("versao").GetString().Should().Be("v1");
        leiaute.GetProperty("quantidadeDeCampos").GetInt32().Should().Be(campos);
        leiaute.GetProperty("hash").GetString().Should().HaveLength(64);

        using HttpRequestMessage todos = Requisicao(Rota, "plataforma-admin");
        using JsonDocument ambos = JsonDocument.Parse(await (await client.SendAsync(todos)).Content.ReadAsStringAsync());
        ambos.RootElement.GetArrayLength().Should().Be(2);
    }

    [Fact(DisplayName = "tipo desconhecido é 400")]
    public async Task Listar_TipoDesconhecido_Recusa()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = Requisicao(Rota + "?tipo=outro", "plataforma-admin");

        (await client.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
