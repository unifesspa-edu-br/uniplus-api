namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposEtapa;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

/// <summary>
/// Contrato HTTP do cadastro de tipos de etapa (UNI-REQ-0015, UNI-REQ-0087): leitura pública de
/// ativos e manutenção exclusiva de <c>plataforma-admin</c>, com código permanentemente reservado.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class TipoEtapaEndpointTests
{
    private readonly ConfiguracaoEndpointFixture _fixture;

    public TipoEtapaEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "GET /api/configuracao/tipos-etapa é público e retorna o vendor MIME")]
    public async Task Listar_Publico_Retorna200ComVendorMime()
    {
        using HttpClient client = _fixture.Factory.CreateDefaultClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri("/api/configuracao/tipos-etapa", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType
            .Should().Be("application/vnd.uniplus.tipo-etapa.v1+json");
    }

    [Fact(DisplayName = "Carga inicial usa UUIDv7 RFC 9562 nos sete tipos semeados")]
    public async Task Listar_TiposSemeados_UsamUuidV7()
    {
        HashSet<string> codigosSemeados = new(StringComparer.Ordinal)
        {
            "PROVA_OBJETIVA", "REDACAO", "ENTREVISTA", "ANALISE_HISTORICO",
            "BANCA_HETEROIDENTIFICACAO", "ANALISE_DOCUMENTAL", "NOTA_ENEM",
        };
        using HttpClient client = _fixture.Factory.CreateDefaultClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri("/api/configuracao/tipos-etapa", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument lista = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Guid[] idsSemeados = lista.RootElement.EnumerateArray()
            .Where(item => codigosSemeados.Contains(item.GetProperty("codigo").GetString()!))
            .Select(item => item.GetProperty("id").GetGuid())
            .ToArray();

        idsSemeados.Should().HaveCount(7);
        idsSemeados.Should().OnlyHaveUniqueItems();
        idsSemeados.Should().OnlyContain(id => EhUuidV7Rfc9562(id),
            "a migration semeia entidades de domínio e o projeto exige UUIDv7");
    }

    [Fact(DisplayName = "POST /api/configuracao/admin/tipos-etapa sem autenticação retorna 401")]
    public async Task Criar_SemAutenticacao_Retorna401()
    {
        using HttpClient client = _fixture.Factory.CreateDefaultClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/configuracao/admin/tipos-etapa", UriKind.Relative));
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "POST autenticado sem role plataforma-admin retorna 403")]
    public async Task Criar_SemRolePlataformaAdmin_Retorna403()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/configuracao/admin/tipos-etapa", UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "candidato");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(new { codigo = CodigoUnico(), nome = "Sem permissão" });

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "POST cria (201), GET público lista e PUT altera somente os dados descritivos")]
    public async Task Criar_ListarEAtualizar_ComAdmin_PersisteCadastro()
    {
        string codigo = CodigoUnico();
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage criar = await EnviarPostAdmin(client, new
        {
            codigo,
            nome = "Seleção de teste",
            descricao = "Descrição inicial",
        });

        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();
        id.Should().NotBe(Guid.Empty);

        HttpResponseMessage listar = await client.GetAsync(new Uri("/api/configuracao/tipos-etapa", UriKind.Relative));
        listar.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument lista = JsonDocument.Parse(await listar.Content.ReadAsStringAsync());
        lista.RootElement.EnumerateArray()
            .Single(item => item.GetProperty("id").GetGuid() == id)
            .GetProperty("codigo").GetString().Should().Be(codigo);

        HttpResponseMessage atualizar = await EnviarPutAdmin(client, id, new
        {
            id,
            nome = "Seleção renomeada",
            descricao = "Descrição atualizada",
        });
        atualizar.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage obter = await client.GetAsync(new Uri($"/api/configuracao/tipos-etapa/{id}", UriKind.Relative));
        using JsonDocument item = JsonDocument.Parse(await obter.Content.ReadAsStringAsync());
        item.RootElement.GetProperty("codigo").GetString().Should().Be(codigo, "o código é a identidade imutável do tipo de etapa");
        item.RootElement.GetProperty("nome").GetString().Should().Be("Seleção renomeada");
        item.RootElement.TryGetProperty("_links", out _).Should().BeTrue("a leitura pública expõe HATEOAS nível 1");
    }

    [Theory(DisplayName = "POST recusa U+0000 nos campos textuais com 422")]
    [InlineData("codigo")]
    [InlineData("nome")]
    [InlineData("descricao")]
    public async Task Criar_CampoTextualComCaractereNulo_Retorna422(string campo)
    {
        string invalido = $"valor{(char)0}invalido";
        object payload = campo switch
        {
            "codigo" => new { codigo = invalido, nome = "Nome válido", descricao = "Descrição válida" },
            "nome" => new { codigo = CodigoUnico(), nome = invalido, descricao = "Descrição válida" },
            "descricao" => new { codigo = CodigoUnico(), nome = "Nome válido", descricao = invalido },
            _ => throw new InvalidOperationException($"Campo de teste inesperado: {campo}"),
        };
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await EnviarPostAdmin(client, payload);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Theory(DisplayName = "PUT recusa U+0000 nos campos editáveis com 422")]
    [InlineData("nome")]
    [InlineData("descricao")]
    public async Task Atualizar_CampoTextualComCaractereNulo_Retorna422(string campo)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarPostAdmin(client, new
        {
            codigo = CodigoUnico(),
            nome = "Nome válido",
            descricao = "Descrição válida",
        });
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();
        string invalido = $"valor{(char)0}invalido";
        object payload = campo switch
        {
            "nome" => new { id, nome = invalido, descricao = "Descrição válida" },
            "descricao" => new { id, nome = "Nome válido", descricao = invalido },
            _ => throw new InvalidOperationException($"Campo de teste inesperado: {campo}"),
        };

        HttpResponseMessage response = await EnviarPutAdmin(client, id, payload);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "ADR-0125: POST com código e nome ausentes ao mesmo tempo devolve as duas violações em errors[], campo em camelCase")]
    public async Task Criar_CodigoENomeAusentes_DevolveAsDuasViolacoesEmErrors()
    {
        var body = new { codigo = "", nome = "" };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPostAdmin(client, body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString().Should().Be("uniplus.configuracao.tipo_etapa.codigo_obrigatorio");

        JsonElement errors = doc.RootElement.GetProperty("errors");
        errors.GetArrayLength().Should().Be(2);
        errors[0].GetProperty("field").GetString().Should().Be("codigo");
        errors[1].GetProperty("field").GetString().Should().Be("nome");
    }

    /// <summary>
    /// ADR-0125: prova que "codigo" genuinamente ausente do JSON (não string
    /// vazia) chega ao domínio como 422 específico, não ao 400 genérico do
    /// ASP.NET — só possível porque
    /// <see cref="Application.Commands.TiposEtapa.CriarTipoEtapaCommand.Codigo"/>
    /// é <c>string?</c>, não <c>string</c>.
    /// </summary>
    [Fact(DisplayName = "ADR-0125: POST com código genuinamente ausente do JSON chega ao domínio como 422 específico")]
    public async Task Criar_CodigoAusenteDoJson_ChegaAoDominioComoViolacaoEspecifica()
    {
        const string json = """{"nome":"Nome válido"}""";

        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/configuracao/admin/tipos-etapa", UriKind.Relative));
        AutenticarComoAdmin(request);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString().Should().Be("uniplus.configuracao.tipo_etapa.codigo_obrigatorio");
    }

    [Fact(DisplayName = "PUT com Id inexistente e Nome vazio devolve 422 (validação vence sobre 404)")]
    public async Task Atualizar_IdInexistenteENomeVazio_Retorna422()
    {
        Guid id = Guid.NewGuid();

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPutAdmin(client, id, new { id, nome = "" });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "DELETE desativa, remove da leitura pública e mantém o código reservado")]
    public async Task Desativar_OcultaDaLeituraPublicaESemReusoDoCodigo()
    {
        string codigo = CodigoUnico();
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage criar = await EnviarPostAdmin(client, new { codigo, nome = "Seleção temporária" });
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();

        HttpResponseMessage desativar = await EnviarDeleteAdmin(client, id);
        desativar.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage obter = await client.GetAsync(new Uri($"/api/configuracao/tipos-etapa/{id}", UriKind.Relative));
        obter.StatusCode.Should().Be(HttpStatusCode.NotFound, "a API pública só expõe itens ativos");

        HttpResponseMessage recriar = await EnviarPostAdmin(client, new { codigo, nome = "Tentativa de reuso" });
        recriar.StatusCode.Should().Be(HttpStatusCode.Conflict, "desativar não libera a identidade regulatória do código");
    }

    private static string CodigoUnico() => $"PS_{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}";

    private static bool EhUuidV7Rfc9562(Guid id)
    {
        string representacao = id.ToString("D");
        return id.Version == 7 && representacao[19] is '8' or '9' or 'a' or 'b';
    }

    private static async Task<HttpResponseMessage> EnviarPostAdmin(HttpClient client, object body)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/configuracao/admin/tipos-etapa", UriKind.Relative));
        AutenticarComoAdmin(request);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> EnviarPutAdmin(HttpClient client, Guid id, object body)
    {
        using HttpRequestMessage request = new(HttpMethod.Put, new Uri($"/api/configuracao/admin/tipos-etapa/{id}", UriKind.Relative));
        AutenticarComoAdmin(request);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> EnviarDeleteAdmin(HttpClient client, Guid id)
    {
        using HttpRequestMessage request = new(HttpMethod.Delete, new Uri($"/api/configuracao/admin/tipos-etapa/{id}", UriKind.Relative));
        AutenticarComoAdmin(request);
        return await client.SendAsync(request);
    }

    private static void AutenticarComoAdmin(HttpRequestMessage request)
    {
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
    }
}
