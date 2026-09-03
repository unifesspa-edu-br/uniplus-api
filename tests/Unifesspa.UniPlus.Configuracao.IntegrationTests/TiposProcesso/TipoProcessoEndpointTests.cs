namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.TiposProcesso;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

/// <summary>
/// Contrato HTTP do cadastro de tipos de processo (UNI-REQ-0098): leitura pública de
/// ativos e manutenção exclusiva de <c>plataforma-admin</c>, com código permanentemente reservado.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class TipoProcessoEndpointTests
{
    private readonly ConfiguracaoEndpointFixture _fixture;

    public TipoProcessoEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "GET /api/configuracao/tipos-processo é público e retorna o vendor MIME")]
    public async Task Listar_Publico_Retorna200ComVendorMime()
    {
        using HttpClient client = _fixture.Factory.CreateDefaultClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri("/api/configuracao/tipos-processo", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType
            .Should().Be("application/vnd.uniplus.tipo-processo.v1+json");
    }

    [Fact(DisplayName = "Carga inicial usa UUIDv7 RFC 9562 nos oito tipos legados")]
    public async Task Listar_TiposLegadosSemeados_UsamUuidV7()
    {
        HashSet<string> codigosLegados = new(StringComparer.Ordinal)
        {
            "SiSU", "PSIQ", "PSECampo", "PSVR", "TransferenciaInterna",
            "TransferenciaExterna", "PortadorDiploma", "Reopcao",
        };
        using HttpClient client = _fixture.Factory.CreateDefaultClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri("/api/configuracao/tipos-processo", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument lista = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Guid[] idsLegados = lista.RootElement.EnumerateArray()
            .Where(item => codigosLegados.Contains(item.GetProperty("codigo").GetString()!))
            .Select(item => item.GetProperty("id").GetGuid())
            .ToArray();

        idsLegados.Should().HaveCount(8);
        idsLegados.Should().OnlyHaveUniqueItems();
        idsLegados.Should().OnlyContain(id => EhUuidV7Rfc9562(id),
            "a migration semeia entidades de domínio e o projeto exige UUIDv7");
    }

    [Fact(DisplayName = "POST /api/configuracao/admin/tipos-processo sem autenticação retorna 401")]
    public async Task Criar_SemAutenticacao_Retorna401()
    {
        using HttpClient client = _fixture.Factory.CreateDefaultClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/configuracao/admin/tipos-processo", UriKind.Relative));
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "POST autenticado sem role plataforma-admin retorna 403")]
    public async Task Criar_SemRolePlataformaAdmin_Retorna403()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/configuracao/admin/tipos-processo", UriKind.Relative));
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

        HttpResponseMessage listar = await client.GetAsync(new Uri("/api/configuracao/tipos-processo", UriKind.Relative));
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

        HttpResponseMessage obter = await client.GetAsync(new Uri($"/api/configuracao/tipos-processo/{id}", UriKind.Relative));
        using JsonDocument item = JsonDocument.Parse(await obter.Content.ReadAsStringAsync());
        item.RootElement.GetProperty("codigo").GetString().Should().Be(codigo, "o código é a identidade imutável do tipo de processo");
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
        doc.RootElement.GetProperty("code").GetString().Should().Be("uniplus.configuracao.tipo_processo.codigo_obrigatorio");

        JsonElement errors = doc.RootElement.GetProperty("errors");
        errors.GetArrayLength().Should().Be(2);
        errors[0].GetProperty("field").GetString().Should().Be("codigo");
        errors[1].GetProperty("field").GetString().Should().Be("nome");
    }

    /// <summary>
    /// ADR-0125: prova que "codigo" genuinamente ausente do JSON (não string
    /// vazia) chega ao domínio como 422 específico, não ao 400 genérico do
    /// ASP.NET — só possível porque
    /// <see cref="Application.Commands.TiposProcesso.CriarTipoProcessoCommand.Codigo"/>
    /// é <c>string?</c>, não <c>string</c>.
    /// </summary>
    [Fact(DisplayName = "ADR-0125: POST com código genuinamente ausente do JSON chega ao domínio como 422 específico")]
    public async Task Criar_CodigoAusenteDoJson_ChegaAoDominioComoViolacaoEspecifica()
    {
        const string json = """{"nome":"Nome válido"}""";

        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/configuracao/admin/tipos-processo", UriKind.Relative));
        AutenticarComoAdmin(request);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString().Should().Be("uniplus.configuracao.tipo_processo.codigo_obrigatorio");
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

        HttpResponseMessage obter = await client.GetAsync(new Uri($"/api/configuracao/tipos-processo/{id}", UriKind.Relative));
        obter.StatusCode.Should().Be(HttpStatusCode.NotFound, "a API pública só expõe itens ativos");

        HttpResponseMessage recriar = await EnviarPostAdmin(client, new { codigo, nome = "Tentativa de reuso" });
        recriar.StatusCode.Should().Be(HttpStatusCode.Conflict, "desativar não libera a identidade regulatória do código");
    }

    [Fact(DisplayName = "POST .../ativacao sem autenticação retorna 401")]
    public async Task Reativar_SemAutenticacao_Retorna401()
    {
        using HttpClient client = _fixture.Factory.CreateDefaultClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri($"/api/configuracao/admin/tipos-processo/{Guid.NewGuid()}/ativacao", UriKind.Relative));
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "POST .../ativacao autenticado sem role plataforma-admin retorna 403")]
    public async Task Reativar_SemRolePlataformaAdmin_Retorna403()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri($"/api/configuracao/admin/tipos-processo/{Guid.NewGuid()}/ativacao", UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "candidato");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "POST .../ativacao devolve o tipo desativado à leitura pública com código e nome intactos")]
    public async Task Reativar_TipoDesativado_VoltaAApareceNaLeituraPublica()
    {
        string codigo = CodigoUnico();
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage criar = await EnviarPostAdmin(client, new { codigo, nome = "Seleção reativável", descricao = "Descrição preservada" });
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();
        (await EnviarDeleteAdmin(client, id)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetAsync(new Uri($"/api/configuracao/tipos-processo/{id}", UriKind.Relative)))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        HttpResponseMessage reativar = await EnviarAtivacaoAdmin(client, id);

        reativar.StatusCode.Should().Be(HttpStatusCode.NoContent);
        HttpResponseMessage obter = await client.GetAsync(new Uri($"/api/configuracao/tipos-processo/{id}", UriKind.Relative));
        obter.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument item = JsonDocument.Parse(await obter.Content.ReadAsStringAsync());
        item.RootElement.GetProperty("codigo").GetString().Should().Be(codigo, "reativar não é caminho lateral para trocar a identidade");
        item.RootElement.GetProperty("nome").GetString().Should().Be("Seleção reativável");
        item.RootElement.GetProperty("descricao").GetString().Should().Be("Descrição preservada");
        item.RootElement.GetProperty("ativo").GetBoolean().Should().BeTrue();
    }

    [Fact(DisplayName = "POST .../ativacao sobre tipo já ativo retorna 422 com o código de erro específico")]
    public async Task Reativar_TipoJaAtivo_Retorna422()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarPostAdmin(client, new { codigo = CodigoUnico(), nome = "Seleção ativa" });
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();

        HttpResponseMessage response = await EnviarAtivacaoAdmin(client, id);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString().Should().Be("uniplus.configuracao.tipo_processo.ja_ativo");
    }

    [Fact(DisplayName = "POST .../ativacao com Id inexistente retorna 404")]
    public async Task Reativar_IdInexistente_Retorna404()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await EnviarAtivacaoAdmin(client, Guid.NewGuid());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "GET admin/tipos-processo enxerga o desativado que a leitura pública oculta")]
    public async Task ListarParaManutencao_IncluiDesativado_QueALeituraPublicaOculta()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarPostAdmin(client, new { codigo = CodigoUnico(), nome = "Seleção a desativar" });
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();
        await EnviarDeleteAdmin(client, id);

        Guid[] idsAdmin = await LerIdsAsync(client, await EnviarGetAdmin(client, "/api/configuracao/admin/tipos-processo?limit=100"));
        Guid[] idsPublicos = await LerIdsAsync(client, await client.GetAsync(new Uri("/api/configuracao/tipos-processo?limit=100", UriKind.Relative)));

        idsAdmin.Should().Contain(id, "a visão de manutenção precisa enxergar o desativado para poder reativá-lo");
        idsPublicos.Should().NotContain(id, "a leitura pública só expõe itens ativos");
    }

    [Fact(DisplayName = "GET admin/tipos-processo?apenasAtivos=true oculta o desativado")]
    public async Task ListarParaManutencao_ApenasAtivos_OcultaDesativado()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarPostAdmin(client, new { codigo = CodigoUnico(), nome = "Seleção a desativar" });
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();
        await EnviarDeleteAdmin(client, id);

        Guid[] ids = await LerIdsAsync(client, await EnviarGetAdmin(client, "/api/configuracao/admin/tipos-processo?limit=100&apenasAtivos=true"));

        ids.Should().NotContain(id);
    }

    [Fact(DisplayName = "GET admin/tipos-processo autenticado sem role plataforma-admin retorna 403")]
    public async Task ListarParaManutencao_SemRolePlataformaAdmin_Retorna403()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, new Uri("/api/configuracao/admin/tipos-processo", UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "candidato");

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "GET admin/tipos-processo/{id} abre o desativado que a leitura pública por id oculta")]
    public async Task ObterParaManutencao_TipoDesativado_Retorna200EnquantoARotaPublicaDa404()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarPostAdmin(client, new { codigo = CodigoUnico(), nome = "Seleção desativada" });
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();
        await EnviarDeleteAdmin(client, id);

        HttpResponseMessage manutencao = await EnviarGetAdmin(client, $"/api/configuracao/admin/tipos-processo/{id}");
        HttpResponseMessage publica = await client.GetAsync(new Uri($"/api/configuracao/tipos-processo/{id}", UriKind.Relative));

        manutencao.StatusCode.Should().Be(HttpStatusCode.OK);
        publica.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using JsonDocument item = JsonDocument.Parse(await manutencao.Content.ReadAsStringAsync());
        item.RootElement.GetProperty("ativo").GetBoolean().Should().BeFalse();
    }

    [Fact(DisplayName = "Links da listagem de manutenção resolvem: o self do desativado não cai em 404")]
    public async Task ListarParaManutencao_SelfDoDesativado_Resolve()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarPostAdmin(client, new { codigo = CodigoUnico(), nome = "Seleção desativada" });
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();
        await EnviarDeleteAdmin(client, id);

        HttpResponseMessage listagem = await EnviarGetAdmin(client, "/api/configuracao/admin/tipos-processo?limit=100");
        listagem.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument lista = JsonDocument.Parse(await listagem.Content.ReadAsStringAsync());
        string self = lista.RootElement.EnumerateArray()
            .Single(item => item.GetProperty("id").GetGuid() == id)
            .GetProperty("_links").GetProperty("self").GetString()!;

        HttpResponseMessage seguindoOSelf = await EnviarGetAdmin(client, self);

        self.Should().Be($"/api/configuracao/admin/tipos-processo/{id}");
        seguindoOSelf.StatusCode.Should().Be(HttpStatusCode.OK, "o self de um item desativado precisa levar a uma representação que existe");
    }

    private static async Task<Guid[]> LerIdsAsync(HttpClient client, HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument lista = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return [.. lista.RootElement.EnumerateArray().Select(item => item.GetProperty("id").GetGuid())];
    }

    private static async Task<HttpResponseMessage> EnviarGetAdmin(HttpClient client, string rota)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, new Uri(rota, UriKind.Relative));
        AutenticarComoAdmin(request);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> EnviarAtivacaoAdmin(HttpClient client, Guid id)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri($"/api/configuracao/admin/tipos-processo/{id}/ativacao", UriKind.Relative));
        AutenticarComoAdmin(request);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return await client.SendAsync(request);
    }

    private static string CodigoUnico() => $"PS_{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}";

    private static bool EhUuidV7Rfc9562(Guid id)
    {
        string representacao = id.ToString("D");
        return id.Version == 7 && representacao[19] is '8' or '9' or 'a' or 'b';
    }

    private static async Task<HttpResponseMessage> EnviarPostAdmin(HttpClient client, object body)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/configuracao/admin/tipos-processo", UriKind.Relative));
        AutenticarComoAdmin(request);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> EnviarPutAdmin(HttpClient client, Guid id, object body)
    {
        using HttpRequestMessage request = new(HttpMethod.Put, new Uri($"/api/configuracao/admin/tipos-processo/{id}", UriKind.Relative));
        AutenticarComoAdmin(request);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> EnviarDeleteAdmin(HttpClient client, Guid id)
    {
        using HttpRequestMessage request = new(HttpMethod.Delete, new Uri($"/api/configuracao/admin/tipos-processo/{id}", UriKind.Relative));
        AutenticarComoAdmin(request);
        return await client.SendAsync(request);
    }

    private static void AutenticarComoAdmin(HttpRequestMessage request)
    {
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
    }
}
