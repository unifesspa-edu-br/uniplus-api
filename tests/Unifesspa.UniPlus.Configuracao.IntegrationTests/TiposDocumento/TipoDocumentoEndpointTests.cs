namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposDocumento;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

/// <summary>
/// Smoke + caminho de escrita dos endpoints de <c>TipoDocumento</c> (UNI-REQ-0013):
/// routing, vendor media type, HATEOAS, autenticação/autorização, idempotência,
/// forma e existência da categoria no cadastro (422), unicidade do código (409) e contrato
/// classificatório puro (sem regra material) — com Wolverine contra Postgres efêmero.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class TipoDocumentoEndpointTests
{
    private readonly ConfiguracaoEndpointFixture _fixture;

    public TipoDocumentoEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "GET /api/configuracao/tipos-documento retorna 200 com Content-Type vendor MIME")]
    public async Task Listar_Retorna200ComVendorMime()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri("/api/configuracao/tipos-documento", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType
            .Should().Be("application/vnd.uniplus.tipo-documento.v1+json");
    }

    [Fact(DisplayName = "GET /api/configuracao/tipos-documento/{id} retorna 404 quando inexistente")]
    public async Task ObterPorId_NaoExiste_Retorna404()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri($"/api/configuracao/tipos-documento/{Guid.NewGuid()}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "POST /api/configuracao/admin/tipos-documento sem autenticação retorna 401")]
    public async Task Criar_SemAuth_Retorna401()
    {
        using HttpClient client = _fixture.Factory.CreateDefaultClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/configuracao/admin/tipos-documento", UriKind.Relative));
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "POST autenticado sem role plataforma-admin retorna 403")]
    public async Task Criar_SemRoleAdmin_Retorna403()
    {
        var body = new { codigo = CodigoUnico(), nome = "Sem permissão", categoria = "OUTROS" };

        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/configuracao/admin/tipos-documento", UriKind.Relative));
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
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/configuracao/admin/tipos-documento", UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "POST cria (201) e o GET subsequente retorna os campos classificatórios + HATEOAS")]
    public async Task Criar_ComAuthEIdempotency_Retorna201EPersiste()
    {
        string codigo = CodigoUnico();
        var body = new
        {
            codigo,
            nome = "Laudo médico",
            categoria = "SAUDE",
            descricao = "Documento de saúde",
            formatosAceitos = "pdf,jpg",
            tamanhoMaximoMb = 10,
        };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarPostAdmin(client, body);

        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();
        id.Should().NotBe(Guid.Empty);

        HttpResponseMessage obter = await client.GetAsync(
            new Uri($"/api/configuracao/tipos-documento/{id}", UriKind.Relative));
        obter.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument doc = JsonDocument.Parse(await obter.Content.ReadAsStringAsync());
        JsonElement root = doc.RootElement;
        root.GetProperty("codigo").GetString().Should().Be(codigo);
        root.GetProperty("nome").GetString().Should().Be("Laudo médico");
        root.GetProperty("categoria").GetString().Should().Be("SAUDE");
        root.GetProperty("tamanhoMaximoMb").GetInt32().Should().Be(10);
        root.TryGetProperty("_links", out _).Should().BeTrue("HATEOAS Level 1 expõe _links.self (ADR-0029)");
    }

    [Fact(DisplayName = "Contrato classificatório puro: o recurso não expõe nenhuma regra material (validade/assinatura/lideranças)")]
    public async Task Criar_RecursoNaoExpoeRegraMaterial()
    {
        var body = new { codigo = CodigoUnico(), nome = "Comprovante de residência", categoria = "RESIDENCIA" };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarPostAdmin(client, body);
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();

        HttpResponseMessage obter = await client.GetAsync(
            new Uri($"/api/configuracao/tipos-documento/{id}", UriKind.Relative));
        using JsonDocument doc = JsonDocument.Parse(await obter.Content.ReadAsStringAsync());

        // A separação central da #591: o tipo diz "o que um documento é", nunca uma
        // regra material (essas vivem na exigência do edital ou na homologação).
        string[] propriedadesMateriaisProibidas =
            ["validade", "validadeMeses", "idadeMaximaMeses", "exigeAssinatura", "exigeAssinaturaDigital",
             "numeroAssinaturas", "lideranças", "lideranca", "aplicabilidade", "consequenciaIndeferimento"];
        foreach (string proibida in propriedadesMateriaisProibidas)
        {
            doc.RootElement.TryGetProperty(proibida, out _)
                .Should().BeFalse($"o tipo de documento é classificatório puro e não deve expor '{proibida}'");
        }
    }

    [Fact(DisplayName = "POST com categoria fora do formato de código retorna 422 com erro de forma")]
    public async Task Criar_CategoriaForaDoFormato_Retorna422()
    {
        var body = new { codigo = CodigoUnico(), nome = "Categoria mal formada", categoria = "financeiro" };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPostAdmin(client, body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString()
            .Should().Be("uniplus.configuracao.tipo_documento.categoria_formato_invalido");
    }

    [Fact(DisplayName = "POST com categoria bem formada mas ausente do cadastro retorna 422 distinguível do erro de forma")]
    public async Task Criar_CategoriaInexistenteNoCadastro_Retorna422()
    {
        var body = new { codigo = CodigoUnico(), nome = "Categoria inexistente", categoria = "CATEGORIA_QUE_NAO_EXISTE" };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPostAdmin(client, body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString()
            .Should().Be("uniplus.configuracao.tipo_documento.categoria_nao_encontrada",
                "o operador precisa distinguir 'escrevi errado' de 'essa categoria não existe'");
    }

    [Fact(DisplayName = "POST com categoria removida do cadastro retorna 422")]
    public async Task Criar_CategoriaRemovidaDoCadastro_Retorna422()
    {
        string codigoCategoria = $"CAT_{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}";

        using HttpClient client = _fixture.Factory.CreateClient();
        Guid categoriaId = await CriarCategoria(client, codigoCategoria);

        HttpResponseMessage aceito = await EnviarPostAdmin(
            client, new { codigo = CodigoUnico(), nome = "Antes da remoção", categoria = codigoCategoria });
        aceito.StatusCode.Should().Be(HttpStatusCode.Created, "a categoria estava viva");

        using HttpRequestMessage remover = new(
            HttpMethod.Delete, new Uri($"/api/configuracao/admin/categorias-documento/{categoriaId}", UriKind.Relative));
        AutenticarComoAdmin(remover);
        (await client.SendAsync(remover)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage recusado = await EnviarPostAdmin(
            client, new { codigo = CodigoUnico(), nome = "Depois da remoção", categoria = codigoCategoria });

        recusado.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "só categoria viva vale — o soft-delete tira a categoria de circulação para novos tipos");
    }

    [Fact(DisplayName = "Categoria com espaço em volta é persistida normalizada, não só comparada normalizada")]
    public async Task Criar_CategoriaComEspaco_PersisteNormalizada()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarPostAdmin(
            client, new { codigo = CodigoUnico(), nome = "Comprovante de renda", categoria = " RENDA " });

        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();

        HttpResponseMessage obter = await client.GetAsync(
            new Uri($"/api/configuracao/tipos-documento/{id}", UriKind.Relative));
        using JsonDocument doc = JsonDocument.Parse(await obter.Content.ReadAsStringAsync());

        doc.RootElement.GetProperty("categoria").GetString().Should().Be("RENDA",
            "o valor que persiste é o normalizado — a comparação a jusante é ordinal");
    }

    [Fact(DisplayName = "Categoria com o tamanho máximo do cadastro é aceita no tipo de documento")]
    public async Task Criar_CategoriaNoTamanhoMaximo_Aceita()
    {
        // O cadastro aceita código de até 50 caracteres; a coluna do tipo de documento
        // nasceu com 30, dimensionada para os sete tokens do enum antigo. Sem a
        // ampliação, esta categoria legítima viraria erro de banco (500) em vez de
        // cadastro aceito.
        string categoriaLonga = $"CAT_{new string('X', 46)}";

        using HttpClient client = _fixture.Factory.CreateClient();
        await CriarCategoria(client, categoriaLonga);

        HttpResponseMessage response = await EnviarPostAdmin(
            client, new { codigo = CodigoUnico(), nome = "Tipo de categoria longa", categoria = categoriaLonga });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid id = await response.Content.ReadFromJsonAsync<Guid>();

        HttpResponseMessage obter = await client.GetAsync(
            new Uri($"/api/configuracao/tipos-documento/{id}", UriKind.Relative));
        using JsonDocument doc = JsonDocument.Parse(await obter.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("categoria").GetString().Should().Be(categoriaLonga);
    }

    [Fact(DisplayName = "POST com código já existente entre vivos retorna 409")]
    public async Task Criar_CodigoDuplicado_Retorna409()
    {
        string codigo = CodigoUnico();
        var body = new { codigo, nome = "Primeiro", categoria = "OUTROS" };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage primeiro = await EnviarPostAdmin(client, body);
        primeiro.StatusCode.Should().Be(HttpStatusCode.Created);

        HttpResponseMessage segundo = await EnviarPostAdmin(client, new { codigo, nome = "Segundo", categoria = "OUTROS" });
        segundo.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact(DisplayName = "ADR-0125: POST com nome vazio e categoria mal formada devolve as duas violações em errors[], campo em camelCase")]
    public async Task Criar_NomeVazioECategoriaMalFormada_DevolveAsDuasViolacoesEmErrors()
    {
        var body = new { codigo = CodigoUnico(), nome = "", categoria = "financeiro" };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPostAdmin(client, body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString().Should().Be("uniplus.configuracao.tipo_documento.nome_obrigatorio");

        JsonElement errors = doc.RootElement.GetProperty("errors");
        errors.GetArrayLength().Should().Be(2);
        errors[0].GetProperty("field").GetString().Should().Be("nome");
        errors[1].GetProperty("field").GetString().Should().Be("categoria");
    }

    /// <summary>
    /// ADR-0125: prova que "codigo" genuinamente ausente do JSON (não string
    /// vazia) chega ao domínio como 422 específico, não ao 400 genérico do
    /// ASP.NET — só possível porque
    /// <see cref="Application.Commands.TiposDocumento.CriarTipoDocumentoCommand.Codigo"/>
    /// é <c>string?</c>, não <c>string</c>.
    /// </summary>
    [Fact(DisplayName = "ADR-0125: POST com código genuinamente ausente do JSON chega ao domínio como 422 específico")]
    public async Task Criar_CodigoAusenteDoJson_ChegaAoDominioComoViolacaoEspecifica()
    {
        const string json = """{"nome":"Documento de teste","categoria":"OUTROS"}""";

        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/configuracao/admin/tipos-documento", UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString().Should().Be("uniplus.configuracao.tipo_documento.codigo_obrigatorio");
    }

    [Fact(DisplayName = "PUT com Id inexistente e Nome vazio devolve 422 (validação vence sobre 404)")]
    public async Task Atualizar_IdInexistenteENomeVazio_Retorna422()
    {
        Guid id = Guid.NewGuid();
        var body = new { id, codigo = CodigoUnico(), nome = "", categoria = "OUTROS" };

        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Put, new Uri($"/api/configuracao/admin/tipos-documento/{id}", UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(body);

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    private static string CodigoUnico() => $"DOC_{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}";

    private static void AutenticarComoAdmin(HttpRequestMessage request)
    {
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
    }

    /// <summary>Cria uma categoria pelo cadastro e devolve o Id, para os testes que precisam removê-la depois.</summary>
    private static async Task<Guid> CriarCategoria(HttpClient client, string codigo)
    {
        using HttpRequestMessage request = new(
            HttpMethod.Post, new Uri("/api/configuracao/admin/categorias-documento", UriKind.Relative));
        AutenticarComoAdmin(request);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(new { codigo, nome = "Categoria de teste" });

        HttpResponseMessage response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await response.Content.ReadFromJsonAsync<Guid>();
    }

    private static async Task<HttpResponseMessage> EnviarPostAdmin(HttpClient client, object body)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/configuracao/admin/tipos-documento", UriKind.Relative));
        AutenticarComoAdmin(request);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }
}
