namespace Unifesspa.UniPlus.Selecao.IntegrationTests.RolDeRegras;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Outbox.Cascading;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Contrato HTTP da leitura do <c>rol_de_regras</c>: as rotas existem, exigem autenticação,
/// filtram pelo código canônico de tipo, resolvem a identidade <c>(codigo, versao)</c> e não
/// têm par de escrita.
/// </summary>
[Collection(CascadingCollection.Name)]
public sealed class RegrasCatalogoEndpointTests
{
    private const string Rota = "/api/selecao/regras-catalogo";

    private readonly CascadingFixture _fixture;

    public RegrasCatalogoEndpointTests(CascadingFixture fixture)
    {
        _fixture = fixture;
    }

    private static HttpRequestMessage Autenticada(HttpMethod metodo, string url)
    {
        HttpRequestMessage request = new(metodo, new Uri(url, UriKind.Relative));
        request.Headers.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
        return request;
    }

    private static async Task<JsonDocument> ListarAsync(HttpClient client, string query = "")
    {
        using HttpRequestMessage request = Autenticada(HttpMethod.Get, Rota + query);
        HttpResponseMessage response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    [Fact(DisplayName = "GET /regras-catalogo devolve entradas com código, versão, tipo, esquema de argumentos, hash e _links")]
    public async Task Listar_DevolveARepresentacaoCompleta()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        using JsonDocument doc = await ListarAsync(client, "?limit=5");

        JsonElement primeira = doc.RootElement.EnumerateArray().First();
        primeira.GetProperty("codigo").GetString().Should().NotBeNullOrWhiteSpace();
        primeira.GetProperty("versao").GetString().Should().NotBeNullOrWhiteSpace();
        primeira.GetProperty("tipo").GetString().Should().NotBeNullOrWhiteSpace();
        primeira.GetProperty("hash").GetString().Should().HaveLength(64);
        primeira.GetProperty("baseLegal").GetString().Should().NotBeNull();

        // esquemaArgs e invariantes atravessam como JSON, não como texto escapado — o cliente
        // não precisa desserializar de novo o que já era JSON.
        primeira.GetProperty("esquemaArgs").ValueKind.Should().NotBe(JsonValueKind.String);
        primeira.GetProperty("invariantes").ValueKind.Should().NotBe(JsonValueKind.String);

        primeira.GetProperty("_links").GetProperty("self").GetString()
            .Should().Contain("/versoes/", "o self aponta para a versão, que é o que um rascunho referencia");
    }

    /// <summary>
    /// O rol chega ao cliente como campo tipado, não como leitura de dentro de
    /// <c>esquemaArgs</c> — o esquema é aberto por contrato, e ler uma chave nomeada de dentro
    /// dele criaria acoplamento que nenhum gate protege.
    /// </summary>
    [Fact(DisplayName = "modalidadesAdmitidas: rol fechado como array tipado, rol aberto como null")]
    public async Task Listar_ExpoeModalidadesAdmitidasTipado()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        using JsonDocument doc = await ListarAsync(
            client, $"?tipo={TipoRegraCodigo.RegraDistribuicaoVagas}&limit=10");

        JsonElement[] regras = [.. doc.RootElement.EnumerateArray()];

        JsonElement psiq = regras.Single(r => r.GetProperty("codigo").GetString() == RegraDistribuicaoVagasCodigo.Psiq);
        JsonElement modalidadesPsiq = psiq.GetProperty("modalidadesAdmitidas");
        modalidadesPsiq.ValueKind.Should().Be(JsonValueKind.Array);
        modalidadesPsiq.EnumerateArray().Select(m => m.GetString())
            .Should().BeEquivalentTo(["AC_I", "AC_Q"]);

        JsonElement institucional = regras.Single(r => r.GetProperty("codigo").GetString() == RegraDistribuicaoVagasCodigo.Institucional);
        institucional.GetProperty("modalidadesAdmitidas").ValueKind.Should().Be(
            JsonValueKind.Null, "rol aberto — a regra institucional não restringe o conjunto");
    }

    [Fact(DisplayName = "O filtro aceita o mesmo código de tipo que a resposta devolve — quem relê um valor da API e o usa como filtro é atendido")]
    public async Task Listar_FiltroAceitaOCodigoQueARespostaDevolve()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        using JsonDocument doc = await ListarAsync(client, $"?tipo={TipoRegraCodigo.CriterioDesempate}");

        doc.RootElement.EnumerateArray().Should().NotBeEmpty();
        doc.RootElement.EnumerateArray()
            .Should().AllSatisfy(r => r.GetProperty("tipo").GetString()
                .Should().Be(TipoRegraCodigo.CriterioDesempate));
    }

    [Fact(DisplayName = "Tipo desconhecido recusa com 400, em vez de devolver lista vazia como se não houvesse regra daquele tipo")]
    public async Task Listar_TipoDesconhecido_Recusa()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = Autenticada(HttpMethod.Get, $"{Rota}?tipo=regra_inventada");

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "lista vazia diria 'não há regra desse tipo' a quem, na verdade, escreveu um tipo que não existe");
    }

    [Fact(DisplayName = "GET por (codigo, versao) devolve a definição com o mesmo hash que a listagem publica")]
    public async Task Obter_PorIdentidade_DevolveAMesmaDefinicao()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using JsonDocument lista = await ListarAsync(client, "?limit=1");
        JsonElement esperada = lista.RootElement.EnumerateArray().First();
        string codigo = esperada.GetProperty("codigo").GetString()!;
        string versao = esperada.GetProperty("versao").GetString()!;

        using HttpRequestMessage request = Autenticada(HttpMethod.Get, $"{Rota}/{Uri.EscapeDataString(codigo)}/versoes/{versao}");
        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument detalhe = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        detalhe.RootElement.GetProperty("hash").GetString()
            .Should().Be(esperada.GetProperty("hash").GetString());
    }

    [Fact(DisplayName = "Versão inexistente de um código conhecido responde 404 com ProblemDetails canônico")]
    public async Task Obter_VersaoInexistente_Retorna404()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using JsonDocument lista = await ListarAsync(client, "?limit=1");
        string codigo = lista.RootElement.EnumerateArray().First().GetProperty("codigo").GetString()!;

        using HttpRequestMessage request = Autenticada(HttpMethod.Get, $"{Rota}/{Uri.EscapeDataString(codigo)}/versoes/v999");
        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using JsonDocument problema = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        problema.RootElement.GetProperty("code").GetString()
            .Should().Be("uniplus.selecao.regra_catalogo.nao_encontrada");
    }

    [Fact(DisplayName = "Leitura sem autenticação é recusada — o catálogo acompanha a autorização da configuração que o consome")]
    public async Task Listar_SemAutenticacao_Recusa()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(new Uri(Rota, UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "Autenticado sem plataforma-admin é recusado com 403 — autenticar não é o mesmo que poder configurar um certame")]
    public async Task Listar_AutenticadoSemPapelAdministrativo_Recusa()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, new Uri(Rota, UriKind.Relative));
        request.Headers.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "candidato");

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "o catálogo acompanha a autorização da configuração que o consome; um perfil de candidato "
            + "autenticado não tem o que fazer com o esquema de argumentos de uma regra de distribuição");
    }

    [Theory(DisplayName = "Catálogo seed-governado não tem rota de escrita — evoluir uma regra é publicar versão nova por migration")]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task Catalogo_NaoTemRotaDeEscrita(string verbo)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = Autenticada(new HttpMethod(verbo), Rota);
        if (verbo != "DELETE")
        {
            request.Content = JsonContent.Create(new { codigo = "X", versao = "v1" });
        }

        HttpResponseMessage response = await client.SendAsync(request);

        // 405 é a resposta de quem tem a rota e não o verbo. Um 401/403 aqui significaria que a
        // rota de escrita existe e só está protegida — e uma proteção se afrouxa.
        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }
}
