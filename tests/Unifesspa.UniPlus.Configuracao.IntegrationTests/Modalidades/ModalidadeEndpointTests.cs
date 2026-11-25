namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Modalidades;

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

/// <summary>
/// Smoke + caminho de escrita dos endpoints de <c>Modalidade</c> (UNI-REQ-0011):
/// routing, vendor media type, HATEOAS, autenticação/autorização, idempotência,
/// domínio fechado dos enums (422), coerência de domínio (422) e unicidade do
/// código (409) — com Wolverine contra Postgres efêmero.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class ModalidadeEndpointTests
{
    private const string BaseLegalDaRedacao = "Lei 12.711/2012, art. 3º (red. Lei 14.723/2023)";
    private const string RegraDeAplicabilidadeDaCota =
        """[[{"fato":"OPTOU_PELAS_COTAS_DA_LEI","operador":"IGUAL","valor":true}]]""";

    private readonly ConfiguracaoEndpointFixture _fixture;

    public ModalidadeEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "GET /api/configuracao/modalidades retorna 200 com Content-Type vendor MIME")]
    public async Task Listar_Retorna200ComVendorMime()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri("/api/configuracao/modalidades", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType
            .Should().Be("application/vnd.uniplus.modalidade.v1+json");
    }

    [Fact(DisplayName = "GET /api/configuracao/modalidades/{id} retorna 404 quando inexistente")]
    public async Task ObterPorId_NaoExiste_Retorna404()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri($"/api/configuracao/modalidades/{Guid.NewGuid()}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "POST /api/configuracao/admin/modalidades sem autenticação retorna 401")]
    public async Task Criar_SemAuth_Retorna401()
    {
        using HttpClient client = _fixture.Factory.CreateDefaultClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/configuracao/admin/modalidades", UriKind.Relative));
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "POST sem Idempotency-Key retorna 400")]
    public async Task Criar_SemIdempotencyKey_Retorna400()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/configuracao/admin/modalidades", UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "POST cria a modalidade com autenticação e chave de idempotência e retorna 201")]
    public async Task Criar_ComAuthEIdempotency_Retorna201()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarPostAdmin(client, CotaPpi() with { Codigo = CodigoUnico() });

        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        (await criar.Content.ReadFromJsonAsync<Guid>()).Should().NotBe(Guid.Empty);
    }

    [Fact(DisplayName = "GET da modalidade criada retorna os campos, o código dos enums e os links HATEOAS")]
    public async Task Obter_ModalidadeCriada_RetornaCamposELinks()
    {
        string codigo = CodigoUnico();
        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarPostAdmin(client, CotaPpi() with { Codigo = codigo });
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();

        JsonElement root = await LerModalidade(client, id);

        root.GetProperty("codigo").GetString().Should().Be(codigo);
        ConferirEstruturaDaCota(root);
        root.GetProperty("regraDeAplicabilidade").GetRawText().Should().Be(RegraDeAplicabilidadeDaCota);
        root.TryGetProperty("_links", out _).Should().BeTrue("HATEOAS Level 1 expõe _links.self (ADR-0029)");
    }

    [Fact(DisplayName = "GET informa se a modalidade é do catálogo legal fixo")]
    public async Task Obter_InformaSeELegalFixa()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarPostAdmin(
            client, new { codigo = CodigoUnico(), naturezaLegal = "AMPLA", regraDeAplicabilidade = Incondicional });
        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid institucional = await criar.Content.ReadFromJsonAsync<Guid>();

        (await LerLegalFixa(client, IdSemeado("AC"))).Should().BeTrue();
        (await LerLegalFixa(client, institucional)).Should().BeFalse();
    }

    private static async Task<bool> LerLegalFixa(HttpClient client, Guid id) =>
        (await LerModalidade(client, id)).GetProperty("legalFixa").GetBoolean();

    [Fact(DisplayName = "POST com natureza fora do domínio fechado retorna 422")]
    public async Task Criar_NaturezaInvalida_Retorna422()
    {
        var body = new { codigo = CodigoUnico(), naturezaLegal = "COTA" };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPostAdmin(client, body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "POST RETIRADA_DE_OUTRA_MODALIDADE sem origem retorna 422 (coerência de domínio)")]
    public async Task Criar_RetiradaDeOutraModalidadeSemOrigem_Retorna422()
    {
        var body = new { codigo = CodigoUnico(), naturezaLegal = "AMPLA", composicaoVagas = "RETIRADA_DE_OUTRA_MODALIDADE" };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPostAdmin(client, body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "POST sem o campo codigo no JSON retorna 422 (CodigoObrigatorio), não 400 de model binding")]
    public async Task Criar_CodigoAusenteNoJson_Retorna422()
    {
        var body = new { naturezaLegal = "AMPLA", regraDeAplicabilidade = Incondicional };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPostAdmin(client, body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "codigo é string? sem default no comando (ADR-0125) — o [ApiController] não deve " +
            "interceptar o campo ausente antes do domínio rodar");
        (await LerCodigoDeErro(response)).Should().Be("uniplus.configuracao.modalidade.codigo_obrigatorio");
    }

    [Fact(DisplayName = "POST com código já existente entre vivos retorna 409")]
    public async Task Criar_CodigoDuplicado_Retorna409()
    {
        string codigo = CodigoUnico();
        var body = new { codigo, naturezaLegal = "AMPLA", regraDeAplicabilidade = Incondicional };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage primeiro = await EnviarPostAdmin(client, body);
        primeiro.StatusCode.Should().Be(HttpStatusCode.Created);

        HttpResponseMessage segundo = await EnviarPostAdmin(client, body);
        segundo.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact(DisplayName = "POST com código do catálogo legal fixo retorna 409 com o código de erro da reserva")]
    public async Task Criar_CodigoLegalFixo_Retorna409Reservado()
    {
        var body = new { codigo = "LB_PPI", naturezaLegal = "AMPLA", regraDeAplicabilidade = Incondicional };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPostAdmin(client, body);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await LerCodigoDeErro(response)).Should().Be(
            "uniplus.configuracao.modalidade.criacao_bloqueada_codigo_protegido",
            "a reserva do código é resposta distinta de 'já existe uma modalidade com este código'");
    }

    [Theory(DisplayName = "DELETE de modalidade legal fixa retorna 409 e a modalidade continua viva")]
    [InlineData("LB_PPI")]
    public async Task Remover_LegalFixa_Retorna409EPermanece(string codigo)
    {
        Guid id = IdSemeado(codigo);

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarDeleteAdmin(client, id);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await LerCodigoDeErro(response)).Should().Be(
            "uniplus.configuracao.modalidade.remocao_bloqueada_codigo_protegido");
        (await LerLegalFixa(client, id)).Should().BeTrue("a modalidade legal fixa continua no catálogo");
    }

    [Fact(DisplayName = "PUT que altera a estrutura de modalidade legal fixa retorna 422")]
    public async Task Atualizar_LegalFixaEstrutura_Retorna422()
    {
        Guid id = IdSemeado("LI_Q");

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPutAdmin(client, id, new
        {
            id,
            naturezaLegal = "AMPLA",
            composicaoVagas = "RESTANTE_DAS_VAGAS_OFERTADAS",
            regraDeAplicabilidade = Incondicional,
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await LerCodigoDeErro(response)).Should().Be(
            "uniplus.configuracao.modalidade.estrutura_protegida_nao_editavel");
    }

    [Fact(DisplayName = "PUT que altera só descrição e base legal de modalidade legal fixa retorna 204 e persiste, mantendo a estrutura da lei")]
    public async Task Atualizar_LegalFixaRedacao_Retorna204EPersiste()
    {
        Guid id = IdSemeado("LI_EP");
        string descricao = $"Cota — independente de renda, egresso de escola pública ({Guid.NewGuid():N})";

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPutAdmin(
            client, id, CotaPpi() with { Id = id, Descricao = descricao, BaseLegal = BaseLegalDaRedacao });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        JsonElement root = await LerModalidade(client, id);
        root.GetProperty("descricao").GetString().Should().Be(descricao);
        root.GetProperty("baseLegal").GetString().Should().Be(BaseLegalDaRedacao);
        ConferirEstruturaDaCota(root);
    }

    [Fact(DisplayName = "POST sem regra de aplicabilidade retorna 422 com o código da regra obrigatória")]
    public async Task Criar_SemRegraDeAplicabilidade_Retorna422()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPostAdmin(client, new { codigo = CodigoUnico(), naturezaLegal = "AMPLA" });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await LerCodigoDeErro(response)).Should().Be("uniplus.configuracao.modalidade.regra_de_aplicabilidade_obrigatoria");
    }

    [Fact(DisplayName = "POST com regra que cita fato de texto retorna 422 com o código do fato não citável")]
    public async Task Criar_RegraCitaTexto_Retorna422()
    {
        CorpoDaModalidade corpo = CotaPpi() with { Codigo = CodigoUnico(), RegraDeAplicabilidade = RegraQueCitaTexto() };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPostAdmin(client, corpo);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await LerCodigoDeErro(response)).Should().Be("uniplus.configuracao.modalidade.regra_cita_fato_nao_citavel");
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("errors").EnumerateArray().Single().GetProperty("field").GetString()
            .Should().Be("regraDeAplicabilidade[0][1].fato");
    }

    private static readonly object[] Incondicional = [];

    private static object[] OptouPelasCotas() =>
        [new object[] { new { fato = "OPTOU_PELAS_COTAS_DA_LEI", operador = "IGUAL", valor = true } }];

    private static object[] RegraQueCitaTexto() =>
    [
        new object[]
        {
            new { fato = "OPTOU_PELAS_COTAS_DA_LEI", operador = "IGUAL", valor = true },
            new { fato = "NOME", operador = "IGUAL", valor = "VALOR_DE_TESTE" },
        },
    ];

    private static CorpoDaModalidade CotaPpi() => new(
        Descricao: "Cota PPI de baixa renda",
        NaturezaLegal: "COTA_RESERVADA",
        ComposicaoVagas: "DENTRO_DA_RESERVA_LEGAL",
        Remanejamento: new { regra = "SEGUE_ORDEM_DE_REMANEJAMENTO" },
        RegraDeAplicabilidade: OptouPelasCotas(),
        BaseLegal: "Lei 12.711/2012");

    private static void ConferirEstruturaDaCota(JsonElement modalidade)
    {
        modalidade.GetProperty("naturezaLegal").GetProperty("codigo").GetString().Should().Be("COTA_RESERVADA");
        modalidade.GetProperty("composicaoVagas").GetProperty("codigo").GetString().Should().Be("DENTRO_DA_RESERVA_LEGAL");
        modalidade.GetProperty("remanejamento").GetProperty("regra").GetProperty("codigo").GetString().Should().Be("SEGUE_ORDEM_DE_REMANEJAMENTO");
    }

    private static async Task<JsonElement> LerModalidade(HttpClient client, Guid id)
    {
        HttpResponseMessage obter = await client.GetAsync(new Uri($"/api/configuracao/modalidades/{id}", UriKind.Relative));
        obter.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument doc = JsonDocument.Parse(await obter.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    private static async Task<HttpResponseMessage> EnviarDeleteAdmin(HttpClient client, Guid id)
    {
        using HttpRequestMessage request = new(
            HttpMethod.Delete, new Uri($"/api/configuracao/admin/modalidades/{id}", UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
        return await client.SendAsync(request);
    }

    private static Guid IdSemeado(string codigo) =>
        ModalidadeSeed.Itens.Single(i => i.Codigo == codigo).Id;

    private static async Task<string?> LerCodigoDeErro(HttpResponseMessage response)
    {
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("code").GetString();
    }

    private static async Task<HttpResponseMessage> EnviarPutAdmin(HttpClient client, Guid id, object body)
    {
        using HttpRequestMessage request = new(
            HttpMethod.Put, new Uri($"/api/configuracao/admin/modalidades/{id}", UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }

    private static string CodigoUnico() => $"MOD_{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}";

    private static async Task<HttpResponseMessage> EnviarPostAdmin(HttpClient client, object body)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/configuracao/admin/modalidades", UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }

    private sealed record CorpoDaModalidade(
        string? Codigo = null,
        Guid? Id = null,
        string? Descricao = null,
        string? NaturezaLegal = null,
        string? ComposicaoVagas = null,
        object? Remanejamento = null,
        object? RegraDeAplicabilidade = null,
        string? BaseLegal = null);
}
