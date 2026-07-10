namespace Unifesspa.UniPlus.Publicacoes.IntegrationTests.TiposAtoPublicado;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Publicacoes.IntegrationTests.Infrastructure;

/// <summary>
/// Endpoints de <c>TipoAtoPublicado</c> contra o monólito real com Postgres efêmero:
/// routing, vendor media type, HATEOAS, autenticação/autorização, idempotência,
/// conflito de vigência (409) e invariantes de payload (400/422).
/// </summary>
[Collection(PublicacoesEndpointCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class TipoAtoPublicadoEndpointTests
{
    private const string Base = "/api/publicacoes/tipos-ato";
    private const string Admin = "/api/publicacoes/admin/tipos-ato";
    private const string Inicio = "2026-01-01";

    private readonly PublicacoesEndpointFixture _fixture;

    public TipoAtoPublicadoEndpointTests(PublicacoesEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    // ── Leitura pública ──────────────────────────────────────────────────────

    [Fact(DisplayName = "GET tipos-ato retorna 200 com Content-Type vendor MIME")]
    public async Task Listar_Retorna200ComVendorMime()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(new Uri(Base, UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType
            .Should().Be("application/vnd.uniplus.tipo-ato.v1+json");
    }

    [Fact(DisplayName = "GET tipos-ato/{id} retorna 404 quando inexistente")]
    public async Task ObterPorId_NaoExiste_Retorna404()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri($"{Base}/{Guid.NewGuid()}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "GET tipos-ato/{id} devolve _links.self e _links.collection resolvidos")]
    public async Task ObterPorId_TrazLinksResolvidos()
    {
        string codigo = CodigoUnico();
        Guid id = await CriarAsync(codigo);

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync(new Uri($"{Base}/{id}", UriKind.Relative));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement links = doc.RootElement.GetProperty("_links");

        // Asserir o valor, não a presença: um LinkGenerator apontando para a action
        // errada devolveria um path qualquer e passaria por qualquer teste de status.
        links.GetProperty("self").GetString().Should().Be($"{Base}/{id}");
        links.GetProperty("collection").GetString().Should().Be(Base);
    }

    [Fact(DisplayName = "GET tipos-ato traz _links em cada item da coleção")]
    public async Task Listar_ItensTrazemLinks()
    {
        Guid id = await CriarAsync(CodigoUnico());

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync(new Uri($"{Base}?limit=100", UriKind.Relative));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement item = doc.RootElement.EnumerateArray()
            .Single(e => e.GetProperty("id").GetGuid() == id);

        item.GetProperty("_links").GetProperty("self").GetString().Should().Be($"{Base}/{id}");
    }

    [Fact(DisplayName = "A listagem pública não expõe versões de vigência futura")]
    public async Task Listar_NaoExpoeVersaoFutura()
    {
        string codigo = CodigoUnico();
        Guid futura = await CriarAsync(codigo, inicio: "2099-01-01");

        using HttpClient client = _fixture.Factory.CreateClient();

        // Uma versão que ainda não vale é planejamento normativo não anunciado.
        (await IdsAsync(client, $"{Base}?limit=100")).Should().NotContain(futura);

        // A série histórica continua acessível a quem a pedir explicitamente.
        (await IdsAsync(client, $"{Base}?limit=100&vigentes=false")).Should().Contain(futura);
    }

    [Fact(DisplayName = "A listagem pública não expõe versões já encerradas")]
    public async Task Listar_NaoExpoeVersaoEncerrada()
    {
        string codigo = CodigoUnico();
        Guid encerrada = await CriarAsync(codigo, inicio: "2020-01-01", fim: "2021-01-01");

        using HttpClient client = _fixture.Factory.CreateClient();

        (await IdsAsync(client, $"{Base}?limit=100")).Should().NotContain(encerrada);
        (await IdsAsync(client, $"{Base}?limit=100&vigentes=false")).Should().Contain(encerrada);
    }

    [Fact(DisplayName = "A autoria não é exposta no contrato público")]
    public async Task Dto_NaoExpoeAutoria()
    {
        Guid id = await CriarAsync(CodigoUnico());

        using HttpClient client = _fixture.Factory.CreateClient();
        string body = await client.GetStringAsync(new Uri($"{Base}/{id}", UriKind.Relative));

        using JsonDocument doc = JsonDocument.Parse(body);
        doc.RootElement.TryGetProperty("createdBy", out _).Should().BeFalse();
        doc.RootElement.TryGetProperty("updatedBy", out _).Should().BeFalse();
    }

    // ── Vigente ──────────────────────────────────────────────────────────────

    [Fact(DisplayName = "GET tipos-ato/{codigo}/vigente numa data histórica devolve a versão de então")]
    public async Task ObterVigente_DataHistorica_DevolveVersaoDaEpoca()
    {
        string codigo = CodigoUnico();
        Guid antiga = await CriarAsync(codigo, inicio: "2026-01-01", fim: "2026-06-01", congela: false);
        Guid nova = await CriarAsync(codigo, inicio: "2026-06-01", fim: null, congela: true);

        using HttpClient client = _fixture.Factory.CreateClient();

        (await VigenteIdAsync(client, codigo, "2026-03-15")).Should().Be(antiga);
        (await VigenteIdAsync(client, codigo, "2026-05-31")).Should().Be(antiga);

        // O fim é exclusivo: no dia da fronteira já vale a sucessora.
        (await VigenteIdAsync(client, codigo, "2026-06-01")).Should().Be(nova);
    }

    [Fact(DisplayName = "GET vigente sem data usa hoje")]
    public async Task ObterVigente_SemData_UsaHoje()
    {
        string codigo = CodigoUnico();
        Guid id = await CriarAsync(codigo, inicio: "2020-01-01");

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync(
            new Uri($"{Base}/{codigo}/vigente", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("id").GetGuid().Should().Be(id);
    }

    [Fact(DisplayName = "GET vigente de código inexistente retorna 404")]
    public async Task ObterVigente_CodigoInexistente_Retorna404()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri($"{Base}/{CodigoUnico()}/vigente", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory(DisplayName = "GET vigente com código malformado retorna 400, não 404")]
    [InlineData("edital_abertura")]
    [InlineData("Edital")]
    [InlineData("EDITAL-ABERTURA")]
    [InlineData("EDITAL__ABERTURA")]
    public async Task ObterVigente_CodigoMalformado_Retorna400(string codigo)
    {
        // 404 diria "este tipo não existe"; a verdade é "isto não é um código".
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri($"{Base}/{codigo}/vigente", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "GET vigente com um GUID no lugar do código retorna 400")]
    public async Task ObterVigente_ComGuid_Retorna400()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri($"{Base}/{Guid.NewGuid()}/vigente", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Autorização ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "POST admin sem autenticação retorna 401")]
    public async Task Criar_SemAuth_Retorna401()
    {
        using HttpClient client = _fixture.Factory.CreateDefaultClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri(Admin, UriKind.Relative));
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        (await client.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "POST admin autenticado sem role plataforma-admin retorna 403")]
    public async Task Criar_SemRoleAdmin_Retorna403()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri(Admin, UriKind.Relative));
        Autenticar(request, role: "candidato");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(Payload(CodigoUnico()));

        (await client.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "DELETE admin sem autenticação retorna 401")]
    public async Task Remover_SemAuth_Retorna401()
    {
        using HttpClient client = _fixture.Factory.CreateDefaultClient();

        HttpResponseMessage response = await client.DeleteAsync(
            new Uri($"{Admin}/{Guid.NewGuid()}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Idempotência ─────────────────────────────────────────────────────────

    [Fact(DisplayName = "POST sem Idempotency-Key retorna 400")]
    public async Task Criar_SemIdempotencyKey_Retorna400()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri(Admin, UriKind.Relative));
        Autenticar(request);
        request.Content = JsonContent.Create(Payload(CodigoUnico()));

        HttpResponseMessage response = await client.SendAsync(request);

        // Prova que AddIdempotency está registrado: sem ele o filtro não existe e a
        // requisição alcançaria o handler, devolvendo 201.
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "POST repetido com a mesma chave e o mesmo corpo devolve a resposta cacheada")]
    public async Task Criar_ReplayLegitimo_DevolveCache()
    {
        string chave = Guid.NewGuid().ToString();
        object payload = Payload(CodigoUnico());

        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage primeira = await PostAsync(client, payload, chave);
        primeira.StatusCode.Should().Be(HttpStatusCode.Created);
        string corpoPrimeira = await primeira.Content.ReadAsStringAsync();

        HttpResponseMessage segunda = await PostAsync(client, payload, chave);
        segunda.StatusCode.Should().Be(HttpStatusCode.Created);
        (await segunda.Content.ReadAsStringAsync()).Should().Be(corpoPrimeira);
    }

    [Fact(DisplayName = "POST com a mesma chave e corpo diferente retorna 422 body_mismatch")]
    public async Task Criar_MesmaChaveCorpoDiferente_Retorna422()
    {
        string chave = Guid.NewGuid().ToString();

        using HttpClient client = _fixture.Factory.CreateClient();

        (await PostAsync(client, Payload(CodigoUnico()), chave)).StatusCode
            .Should().Be(HttpStatusCode.Created);

        HttpResponseMessage segunda = await PostAsync(client, Payload(CodigoUnico()), chave);

        segunda.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await segunda.Content.ReadAsStringAsync())
            .Should().Contain("uniplus.idempotency.body_mismatch");
    }

    // ── Escrita ──────────────────────────────────────────────────────────────

    [Fact(DisplayName = "POST cria e devolve 201 com Location apontando para o recurso")]
    public async Task Criar_Retorna201ComLocation()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await PostAsync(client, Payload(CodigoUnico()));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location!.AbsolutePath.Should().StartWith($"{Base}/");
    }

    [Fact(DisplayName = "POST com janela sobreposta retorna 409 com o type do ProblemDetails")]
    public async Task Criar_ComSobreposicao_Retorna409()
    {
        string codigo = CodigoUnico();
        await CriarAsync(codigo);

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await PostAsync(client, Payload(codigo));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync())
            .Should().Contain("uniplus.publicacoes.tipo_ato.vigencia_sobreposta");
    }

    [Fact(DisplayName = "POST com janela adjacente é aceito — o fim é exclusivo")]
    public async Task Criar_ComJanelaAdjacente_Retorna201()
    {
        string codigo = CodigoUnico();
        await CriarAsync(codigo, inicio: "2026-01-01", fim: "2026-06-01");

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await PostAsync(
            client, Payload(codigo, inicio: "2026-06-01", fim: null));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact(DisplayName = "POST com código minúsculo retorna 422 — validação do command")]
    public async Task Criar_ComCodigoMinusculo_Retorna422()
    {
        // Falha de FluentValidation no pipeline do Wolverine vira ValidationException,
        // que o GlobalExceptionMiddleware escreve como 422 `uniplus.validacao`. O 400
        // fica para o que o binding recusa antes do command existir (rota, corpo).
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await PostAsync(client, Payload("edital_abertura"));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadAsStringAsync()).Should().Contain("uniplus.validacao");
    }

    [Fact(DisplayName = "POST com janela vazia retorna 422 — o fim é exclusivo")]
    public async Task Criar_ComJanelaVazia_Retorna422()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await PostAsync(
            client, Payload(CodigoUnico(), inicio: Inicio, fim: Inicio));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "PUT atualiza e retorna 204, sem exigir Idempotency-Key")]
    public async Task Atualizar_Retorna204SemIdempotencyKey()
    {
        string codigo = CodigoUnico();
        Guid id = await CriarAsync(codigo);

        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Put, new Uri($"{Admin}/{id}", UriKind.Relative));
        Autenticar(request);
        request.Content = JsonContent.Create(Payload(codigo, id: id, nome: "Nome atualizado"));

        HttpResponseMessage response = await client.SendAsync(request);

        // A ADR-0027 exclui PUT puro do Idempotency-Key: repetir a mesma substituição
        // é inócuo por construção.
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        string body = await client.GetStringAsync(new Uri($"{Base}/{id}", UriKind.Relative));
        using JsonDocument doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("nome").GetString().Should().Be("Nome atualizado");
    }

    [Fact(DisplayName = "PUT com Id divergente retorna 400 com o ProblemDetails canônico")]
    public async Task Atualizar_IdDivergente_Retorna400()
    {
        string codigo = CodigoUnico();
        Guid id = await CriarAsync(codigo);

        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(
            HttpMethod.Put, new Uri($"{Admin}/{Guid.NewGuid()}", UriKind.Relative));
        Autenticar(request);
        request.Content = JsonContent.Create(Payload(codigo, id: id));

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Sem `type`, o cliente não classifica o erro — só lê a mensagem.
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("type").GetString()
            .Should().EndWith("uniplus.publicacoes.tipo_ato.id_divergente");
    }

    [Fact(DisplayName = "PUT de tipo inexistente retorna 404")]
    public async Task Atualizar_Inexistente_Retorna404()
    {
        Guid id = Guid.NewGuid();

        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Put, new Uri($"{Admin}/{id}", UriKind.Relative));
        Autenticar(request);
        request.Content = JsonContent.Create(Payload(CodigoUnico(), id: id));

        (await client.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "DELETE remove, retorna 204 e o tipo some da leitura")]
    public async Task Remover_Retorna204ESomeDaLeitura()
    {
        string codigo = CodigoUnico();
        Guid id = await CriarAsync(codigo);

        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Delete, new Uri($"{Admin}/{id}", UriKind.Relative));
        Autenticar(request);

        (await client.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await client.GetAsync(new Uri($"{Base}/{id}", UriKind.Relative))).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
        (await client.GetAsync(new Uri($"{Base}/{codigo}/vigente", UriKind.Relative))).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "DELETE de tipo inexistente retorna 404")]
    public async Task Remover_Inexistente_Retorna404()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(
            HttpMethod.Delete, new Uri($"{Admin}/{Guid.NewGuid()}", UriKind.Relative));
        Autenticar(request);

        (await client.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Negociação de conteúdo ───────────────────────────────────────────────

    [Fact(DisplayName = "Accept com vendor MIME de versão inexistente retorna 406")]
    public async Task Listar_ComVersaoInexistente_Retorna406()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, new Uri(Base, UriKind.Relative));
        request.Headers.Add("Accept", "application/vnd.uniplus.tipo-ato.v99+json");

        (await client.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.NotAcceptable);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static void Autenticar(HttpRequestMessage request, string role = "plataforma-admin")
    {
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, role);
    }

    private static object Payload(
        string codigo,
        Guid? id = null,
        string nome = "Tipo de ato de teste",
        string inicio = Inicio,
        string? fim = null,
        bool congela = true) =>
        id is { } identificador
            ? new
            {
                id = identificador,
                codigo,
                nome,
                congelaConfiguracao = congela,
                unicoPorObjeto = false,
                efeitoIrreversivel = false,
                vigenciaInicio = inicio,
                vigenciaFim = fim,
            }
            : new
            {
                codigo,
                nome,
                congelaConfiguracao = congela,
                unicoPorObjeto = false,
                efeitoIrreversivel = false,
                vigenciaInicio = inicio,
                vigenciaFim = fim,
            };

    private static async Task<HttpResponseMessage> PostAsync(
        HttpClient client, object payload, string? chave = null)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri(Admin, UriKind.Relative));
        Autenticar(request);
        request.Headers.Add("Idempotency-Key", chave ?? Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(payload);

        return await client.SendAsync(request);
    }

    private async Task<Guid> CriarAsync(
        string codigo, string inicio = Inicio, string? fim = null, bool congela = true)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await PostAsync(client, Payload(codigo, inicio: inicio, fim: fim, congela: congela));
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        return JsonSerializer.Deserialize<Guid>(await response.Content.ReadAsStringAsync());
    }

    private static async Task<Guid> VigenteIdAsync(HttpClient client, string codigo, string data)
    {
        HttpResponseMessage response = await client.GetAsync(
            new Uri($"/api/publicacoes/tipos-ato/{codigo}/vigente?data={data}", UriKind.Relative));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<IReadOnlyList<Guid>> IdsAsync(HttpClient client, string url)
    {
        HttpResponseMessage response = await client.GetAsync(new Uri(url, UriKind.Relative));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return [.. doc.RootElement.EnumerateArray().Select(e => e.GetProperty("id").GetGuid())];
    }

    /// <summary>Código único no formato UPPER_SNAKE, para não colidir entre testes.</summary>
    private static string CodigoUnico()
    {
        string hex = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..12];
        return "ENDPOINT_" + string.Concat(hex.Select(c => (char)('A' + Convert.ToInt32(c.ToString(), 16))));
    }
}
