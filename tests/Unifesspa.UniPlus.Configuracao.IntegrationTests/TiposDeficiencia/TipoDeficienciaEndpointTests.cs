namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposDeficiencia;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

/// <summary>
/// Smoke + caminho de escrita dos endpoints de <c>TipoDeficiencia</c>
/// (UNI-REQ-0012, UNI-REQ-0061): routing, vendor media type, HATEOAS,
/// autenticação/autorização, idempotência, formato fechado do código (422),
/// unicidade do código e do nome (409) e edição do código — com Wolverine contra
/// Postgres efêmero.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class TipoDeficienciaEndpointTests
{
    private const string DescricaoValida = "Deficiência relacionada à visão";

    private readonly ConfiguracaoEndpointFixture _fixture;

    public TipoDeficienciaEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "GET /api/configuracao/tipos-deficiencia retorna 200 com Content-Type vendor MIME")]
    public async Task Listar_Retorna200ComVendorMime()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri("/api/configuracao/tipos-deficiencia", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType
            .Should().Be("application/vnd.uniplus.tipo-deficiencia.v1+json");
    }

    [Fact(DisplayName = "GET /api/configuracao/tipos-deficiencia/{id} retorna 404 quando inexistente")]
    public async Task ObterPorId_NaoExiste_Retorna404()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri($"/api/configuracao/tipos-deficiencia/{Guid.NewGuid()}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "POST /api/configuracao/admin/tipos-deficiencia sem autenticação retorna 401")]
    public async Task Criar_SemAuth_Retorna401()
    {
        using HttpClient client = _fixture.Factory.CreateDefaultClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/configuracao/admin/tipos-deficiencia", UriKind.Relative));
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "POST autenticado sem role plataforma-admin retorna 403")]
    public async Task Criar_SemRoleAdmin_Retorna403()
    {
        var body = new { codigo = CodigoUnico(), nome = NomeUnico(), descricao = DescricaoValida };

        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/configuracao/admin/tipos-deficiencia", UriKind.Relative));
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
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/configuracao/admin/tipos-deficiencia", UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "POST cria (201) e o GET subsequente retorna código/nome/descrição + HATEOAS")]
    public async Task Criar_ComAuthEIdempotency_Retorna201EPersiste()
    {
        string codigo = CodigoUnico();
        string nome = NomeUnico();
        var body = new { codigo, nome, descricao = DescricaoValida };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarPostAdmin(client, body);

        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();
        id.Should().NotBe(Guid.Empty);

        HttpResponseMessage obter = await client.GetAsync(
            new Uri($"/api/configuracao/tipos-deficiencia/{id}", UriKind.Relative));
        obter.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument doc = JsonDocument.Parse(await obter.Content.ReadAsStringAsync());
        JsonElement root = doc.RootElement;
        root.GetProperty("codigo").GetString().Should().Be(codigo, "o código é persistido como informado");
        root.GetProperty("nome").GetString().Should().Be(nome);
        root.GetProperty("descricao").GetString().Should().Be(DescricaoValida);
        root.TryGetProperty("_links", out _).Should().BeTrue("HATEOAS Level 1 expõe _links.self (ADR-0029)");
    }

    [Fact(DisplayName = "POST sem código no JSON retorna 422 com codigo_obrigatorio")]
    public async Task Criar_SemCodigo_Retorna422()
    {
        var body = new { nome = NomeUnico(), descricao = DescricaoValida };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPostAdmin(client, body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await VendorCodeAsync(response)).Should()
            .Be("uniplus.configuracao.tipo_deficiencia.codigo_obrigatorio");
    }

    // Exemplos do Esquema de Cenário da task #1239 — um por motivo de recusa.
    [Theory(DisplayName = "POST com código fora do formato canônico retorna 422 com codigo_formato_invalido")]
    [InlineData("deficiencia_visual")]
    [InlineData("1_DEFICIENCIA")]
    [InlineData("DEFICIÊNCIA_VISUAL")]
    [InlineData("DEFICIENCIA-VISUAL")]
    [InlineData("D")]
    [InlineData("DEFICIENCIA_VISUAL_COM_NOME_LONGO_QUE_PASSA_DE_CINQUENTA")]
    public async Task Criar_CodigoForaDoFormato_Retorna422(string codigo)
    {
        var body = new { codigo, nome = NomeUnico(), descricao = DescricaoValida };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPostAdmin(client, body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await VendorCodeAsync(response)).Should()
            .Be("uniplus.configuracao.tipo_deficiencia.codigo_formato_invalido");
    }

    [Fact(DisplayName = "POST com nome abaixo do tamanho mínimo retorna 422")]
    public async Task Criar_NomeCurto_Retorna422()
    {
        var body = new { codigo = CodigoUnico(), nome = "A", descricao = DescricaoValida };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPostAdmin(client, body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "POST com código já usado por tipo vivo retorna 409 codigo_ja_existe")]
    public async Task Criar_CodigoDuplicado_Retorna409()
    {
        string codigo = CodigoUnico();

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage primeiro = await EnviarPostAdmin(
            client, new { codigo, nome = NomeUnico(), descricao = "Primeiro" });
        primeiro.StatusCode.Should().Be(HttpStatusCode.Created);

        HttpResponseMessage segundo = await EnviarPostAdmin(
            client, new { codigo, nome = NomeUnico(), descricao = "Segundo" });

        segundo.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await VendorCodeAsync(segundo)).Should()
            .Be("uniplus.configuracao.tipo_deficiencia.codigo_ja_existe",
                "o conflito de código precisa ser distinguível de nome_ja_existe");
    }

    [Fact(DisplayName = "POST com nome já usado por tipo vivo retorna 409 nome_ja_existe")]
    public async Task Criar_NomeDuplicado_Retorna409()
    {
        string nome = NomeUnico();

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage primeiro = await EnviarPostAdmin(
            client, new { codigo = CodigoUnico(), nome, descricao = "Primeiro" });
        primeiro.StatusCode.Should().Be(HttpStatusCode.Created);

        HttpResponseMessage segundo = await EnviarPostAdmin(
            client, new { codigo = CodigoUnico(), nome, descricao = "Segundo" });

        segundo.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await VendorCodeAsync(segundo)).Should()
            .Be("uniplus.configuracao.tipo_deficiencia.nome_ja_existe");
    }

    [Fact(DisplayName = "Código de um tipo removido pode ser reutilizado (201) — a unicidade é entre vivos")]
    public async Task Criar_CodigoDeTipoRemovido_Retorna201()
    {
        string codigo = CodigoUnico();

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarPostAdmin(
            client, new { codigo, nome = NomeUnico(), descricao = DescricaoValida });
        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();

        HttpResponseMessage remover = await EnviarDeleteAdmin(client, id);
        remover.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage recriar = await EnviarPostAdmin(
            client, new { codigo, nome = NomeUnico(), descricao = DescricaoValida });

        recriar.StatusCode.Should().Be(HttpStatusCode.Created,
            "o soft-delete libera o slot do índice único parcial");
    }

    [Fact(DisplayName = "POST com código fora do formato e descrição ausente acumula as duas violações em errors[]")]
    public async Task Criar_CodigoInvalidoEDescricaoAusente_AcumulaAsDuasViolacoesEmErrors()
    {
        var body = new { codigo = "deficiencia_visual", nome = NomeUnico(), descricao = "" };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPostAdmin(client, body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement erros = doc.RootElement.GetProperty("errors");
        erros.GetArrayLength().Should().Be(2);
        erros.EnumerateArray().Select(e => e.GetProperty("field").GetString())
            .Should().BeEquivalentTo(["codigo", "descricao"]);
    }

    [Fact(DisplayName = "POST com nome e descrição inválidos ao mesmo tempo acumula as duas violações em errors[]")]
    public async Task Criar_NomeEDescricaoInvalidos_AcumulaAsDuasViolacoesEmErrors()
    {
        var body = new { codigo = CodigoUnico(), nome = "A", descricao = "" };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPostAdmin(client, body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement erros = doc.RootElement.GetProperty("errors");
        erros.GetArrayLength().Should().Be(2);
        erros.EnumerateArray().Select(e => e.GetProperty("field").GetString())
            .Should().BeEquivalentTo(["nome", "descricao"]);
    }

    [Fact(DisplayName = "POST com nome genuinamente ausente no JSON retorna 422 (não 400 de model binding)")]
    public async Task Criar_NomeAusenteNoJson_Retorna422()
    {
        var body = new { codigo = CodigoUnico(), descricao = DescricaoValida };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPostAdmin(client, body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "sem validator FluentValidation a montante, o campo ausente precisa chegar ao domínio (ADR-0125), não virar 400 de model binding");
    }

    [Fact(DisplayName = "PUT alterando só o nome devolve 204 e mantém o código inalterado")]
    public async Task Atualizar_SomenteNome_PreservaCodigo()
    {
        string codigo = CodigoUnico();

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarPostAdmin(
            client, new { codigo, nome = NomeUnico(), descricao = DescricaoValida });
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();

        string novoNome = NomeUnico();
        HttpResponseMessage atualizar = await EnviarPutAdmin(
            client, id, new { id, codigo, nome = novoNome, descricao = DescricaoValida });

        atualizar.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using JsonDocument doc = await ObterAsync(client, id);
        doc.RootElement.GetProperty("codigo").GetString().Should().Be(codigo);
        doc.RootElement.GetProperty("nome").GetString().Should().Be(novoNome);
    }

    [Fact(DisplayName = "PUT alterando o código para outro válido e livre devolve 204 e persiste o novo código")]
    public async Task Atualizar_CodigoLivre_Retorna204EPersiste()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        string nome = NomeUnico();
        HttpResponseMessage criar = await EnviarPostAdmin(
            client, new { codigo = CodigoUnico(), nome, descricao = DescricaoValida });
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();

        string novoCodigo = CodigoUnico();
        HttpResponseMessage atualizar = await EnviarPutAdmin(
            client, id, new { id, codigo = novoCodigo, nome, descricao = DescricaoValida });

        atualizar.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using JsonDocument doc = await ObterAsync(client, id);
        doc.RootElement.GetProperty("codigo").GetString().Should().Be(novoCodigo);
    }

    [Fact(DisplayName = "PUT reenviando o próprio código devolve 204 sem erro de duplicidade")]
    public async Task Atualizar_MesmoCodigo_Retorna204()
    {
        string codigo = CodigoUnico();
        string nome = NomeUnico();

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarPostAdmin(
            client, new { codigo, nome, descricao = DescricaoValida });
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();

        HttpResponseMessage atualizar = await EnviarPutAdmin(
            client, id, new { id, codigo, nome, descricao = "Descrição revisada" });

        atualizar.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact(DisplayName = "PUT para um código já usado por outro tipo vivo retorna 409 codigo_ja_existe")]
    public async Task Atualizar_CodigoColidente_Retorna409()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        string codigoOcupado = CodigoUnico();
        HttpResponseMessage ocupante = await EnviarPostAdmin(
            client, new { codigo = codigoOcupado, nome = NomeUnico(), descricao = DescricaoValida });
        ocupante.StatusCode.Should().Be(HttpStatusCode.Created);

        string nome = NomeUnico();
        HttpResponseMessage criar = await EnviarPostAdmin(
            client, new { codigo = CodigoUnico(), nome, descricao = DescricaoValida });
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();

        HttpResponseMessage atualizar = await EnviarPutAdmin(
            client, id, new { id, codigo = codigoOcupado, nome, descricao = DescricaoValida });

        atualizar.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await VendorCodeAsync(atualizar)).Should()
            .Be("uniplus.configuracao.tipo_deficiencia.codigo_ja_existe");
    }

    private static string CodigoUnico() => $"DEF_{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}";

    private static string NomeUnico() => $"Deficiência {Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}";

    private static async Task<string?> VendorCodeAsync(HttpResponseMessage response)
    {
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.TryGetProperty("code", out JsonElement code)
            ? code.GetString()
            : doc.RootElement.GetProperty("errors")[0].GetProperty("code").GetString();
    }

    private static async Task<JsonDocument> ObterAsync(HttpClient client, Guid id)
    {
        HttpResponseMessage obter = await client.GetAsync(
            new Uri($"/api/configuracao/tipos-deficiencia/{id}", UriKind.Relative));
        obter.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonDocument.Parse(await obter.Content.ReadAsStringAsync());
    }

    private static async Task<HttpResponseMessage> EnviarPostAdmin(HttpClient client, object body)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/configuracao/admin/tipos-deficiencia", UriKind.Relative));
        AplicarCabecalhosAdmin(request);
        request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> EnviarPutAdmin(HttpClient client, Guid id, object body)
    {
        using HttpRequestMessage request = new(HttpMethod.Put, new Uri($"/api/configuracao/admin/tipos-deficiencia/{id}", UriKind.Relative));
        AplicarCabecalhosAdmin(request);
        request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> EnviarDeleteAdmin(HttpClient client, Guid id)
    {
        using HttpRequestMessage request = new(HttpMethod.Delete, new Uri($"/api/configuracao/admin/tipos-deficiencia/{id}", UriKind.Relative));
        AplicarCabecalhosAdmin(request);
        return await client.SendAsync(request);
    }

    private static void AplicarCabecalhosAdmin(HttpRequestMessage request)
    {
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
    }
}
