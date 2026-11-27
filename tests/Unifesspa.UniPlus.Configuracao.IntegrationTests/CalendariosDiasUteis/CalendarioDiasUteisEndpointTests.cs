namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.CalendariosDiasUteis;

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

/// <summary>
/// Smoke + caminho de escrita dos endpoints de <c>CalendarioDiasUteis</c>
/// (UNI-REQ-0116): routing, vendor media type, HATEOAS, autenticação/autorização,
/// idempotência, invariante de vigência única e bloqueio de remoção do calendário
/// vigente, com Wolverine rodando contra Postgres efêmero.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class CalendarioDiasUteisEndpointTests
{
    private const string VendorMime = "application/vnd.uniplus.calendario-dias-uteis.v1+json";
    private const string ColecaoPath = "/api/configuracao/calendarios-dias-uteis";
    private const string AdminPath = "/api/configuracao/admin/calendarios-dias-uteis";

    private readonly ConfiguracaoEndpointFixture _fixture;

    public CalendarioDiasUteisEndpointTests(ConfiguracaoEndpointFixture fixture)
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

        await RespostaDeNaoEncontrado.ConferirAsync(response, "uniplus.configuracao.calendario_dias_uteis.nao_encontrado");
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

    [Fact(DisplayName = "POST admin cria (201 + Location) e o GET subsequente retorna os dias não úteis + _links")]
    public async Task Criar_ComAuthEIdempotency_Retorna201EPersiste()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarPostAdmin(client, AdminPath, CorpoValido(VersaoUnica()));

        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        criar.Headers.Location.Should().NotBeNull();
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();
        id.Should().NotBe(Guid.Empty);

        HttpResponseMessage obter = await client.GetAsync(new Uri($"{ColecaoPath}/{id}", UriKind.Relative));
        obter.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument doc = JsonDocument.Parse(await obter.Content.ReadAsStringAsync());
        JsonElement root = doc.RootElement;
        ConferirCalendarioCriado(root);
    }

    [Theory(DisplayName = "POST admin rejeita snapshot municipal parcial")]
    [InlineData(null, "Marabá", "PA")]
    [InlineData("1504208", null, "PA")]
    [InlineData("1504208", "Marabá", null)]
    [InlineData("1504208", null, null)]
    public async Task Criar_SnapshotMunicipalParcial_Retorna422(
        string? municipioIbge,
        string? municipioNome,
        string? municipioUf)
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await EnviarPostAdmin(
            client,
            AdminPath,
            CorpoComDia("MUNICIPAL", municipioIbge, municipioNome, municipioUf));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "POST admin rejeita U+0000 no nome do snapshot municipal com 422")]
    public async Task Criar_MunicipioNomeComCaractereNulo_Retorna422()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await EnviarPostAdmin(
            client,
            AdminPath,
            CorpoComDia("MUNICIPAL", "1504208", "Mara" + '\0' + "bá", "PA"));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await LerCodigoDeErro(response)).Should().Be("uniplus.cidade_referencia.nome_caractere_invalido");
    }

    [Theory(DisplayName = "POST admin rejeita campos municipais fora de MUNICIPAL")]
    [InlineData("NACIONAL")]
    [InlineData("ESTADUAL")]
    public async Task Criar_SnapshotMunicipalForaDaAbrangencia_Retorna422(string abrangencia)
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await EnviarPostAdmin(
            client,
            AdminPath,
            CorpoComDia(abrangencia, "1504208", "Marabá", "PA", uf: abrangencia == "ESTADUAL" ? "PA" : null));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await LerCodigoDeErro(response)).Should().Be(
            "uniplus.configuracao.calendario_dias_uteis.snapshot_municipal_apenas_para_municipal");
    }

    [Fact(DisplayName = "GET coleção não inclui diasNaoUteis — a listagem projeta só o resumo")]
    public async Task Listar_NaoIncluiDiasNaoUteis()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarPostAdmin(client, AdminPath, CorpoValido(VersaoUnica()));
        criar.StatusCode.Should().Be(HttpStatusCode.Created);

        HttpResponseMessage listar = await client.GetAsync(new Uri(ColecaoPath, UriKind.Relative));

        listar.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument doc = JsonDocument.Parse(await listar.Content.ReadAsStringAsync());
        JsonElement itens = doc.RootElement;
        itens.GetArrayLength().Should().BeGreaterThan(0);
        ConferirResumos(itens);
    }

    [Fact(DisplayName = "POST .../{id}/vigente marca o novo vigente e desmarca o vigente anterior")]
    public async Task MarcarVigente_DesmarcaOAnterior()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        Guid primeiroId = await CriarCalendario(client, VersaoUnica());
        HttpResponseMessage marcarPrimeiro = await EnviarPostAdmin(client, $"{AdminPath}/{primeiroId}/vigente", null);
        marcarPrimeiro.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await ObterVigente(client, primeiroId)).Should().BeTrue();

        Guid segundoId = await CriarCalendario(client, VersaoUnica());
        HttpResponseMessage marcarSegundo = await EnviarPostAdmin(client, $"{AdminPath}/{segundoId}/vigente", null);
        marcarSegundo.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await ObterVigente(client, segundoId)).Should().BeTrue();
        (await ObterVigente(client, primeiroId)).Should().BeFalse("no máximo um calendário é vigente por vez");
    }

    [Fact(DisplayName = "DELETE em calendário vigente retorna 409 com o código de erro NaoRemoveVigente")]
    public async Task Remover_CalendarioVigente_Retorna409()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CriarCalendario(client, VersaoUnica());
        HttpResponseMessage marcarVigente = await EnviarPostAdmin(client, $"{AdminPath}/{id}/vigente", null);
        marcarVigente.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage response = await EnviarDeleteAdmin(client, id);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await LerCodigoDeErro(response)).Should().Be(
            "uniplus.configuracao.calendario_dias_uteis.nao_remove_vigente");
    }

    [Fact(DisplayName = "DELETE em calendário não vigente retorna 204")]
    public async Task Remover_CalendarioNaoVigente_Retorna204()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CriarCalendario(client, VersaoUnica());

        HttpResponseMessage response = await EnviarDeleteAdmin(client, id);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact(DisplayName = "POST com versaoDoCalendario genuinamente ausente no JSON retorna 422 (não 400 de model binding)")]
    public async Task Criar_VersaoDoCalendarioAusenteNoJson_Retorna422()
    {
        object corpo = CorpoSemVersao();

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPostAdmin(client, AdminPath, corpo);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "sem validator FluentValidation a montante, o campo ausente precisa chegar ao domínio (ADR-0125), não virar 400 de model binding");
    }

    [Fact(DisplayName = "POST com violações em dois itens da lista acumula em errors[] com o índice de cada item")]
    public async Task Criar_ViolacoesEmDoisItens_AcumulaEmErrorsComIndiceDeCadaItem()
    {
        object corpo = new { versaoDoCalendario = VersaoUnica(), diasNaoUteis = new[] { DiaInvalido("INVALIDO", "2027-05-08", "Item quebrado"), DiaInvalido("ESTADUAL", "2027-05-09", "Falta UF") } };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarPostAdmin(client, AdminPath, corpo);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement erros = doc.RootElement.GetProperty("errors");
        erros.GetArrayLength().Should().Be(2);
        erros.EnumerateArray().Select(e => e.GetProperty("field").GetString())
            .Should().BeEquivalentTo(["diasNaoUteis[0].abrangencia", "diasNaoUteis[1].uf"]);
    }

    private static void ConferirResumos(JsonElement itens)
    {
        foreach (JsonElement item in itens.EnumerateArray())
        {
            item.TryGetProperty("diasNaoUteis", out _).Should().BeFalse(
                "a listagem não carrega a coleção filha — use ObterPorId para o calendário completo");
        }
    }

    private static void ConferirCalendarioCriado(JsonElement root)
    {
        root.GetProperty("vigente").GetBoolean().Should().BeFalse("todo calendário nasce não vigente");
        JsonElement dias = root.GetProperty("diasNaoUteis");
        dias.GetArrayLength().Should().Be(3);
        dias.EnumerateArray().Should().Contain(d => d.GetProperty("abrangencia").GetProperty("codigo").GetString() == "NACIONAL");
        ConferirDiasRegionais(dias);
        root.TryGetProperty("_links", out _).Should().BeTrue("HATEOAS Level 1 expõe _links.self (ADR-0029)");
    }

    private static void ConferirDiasRegionais(JsonElement dias)
    {
        dias.EnumerateArray().Should().Contain(d =>
            d.GetProperty("abrangencia").GetProperty("codigo").GetString() == "ESTADUAL"
            && d.GetProperty("municipioIbge").ValueKind == JsonValueKind.Null
            && d.GetProperty("municipioNome").ValueKind == JsonValueKind.Null
            && d.GetProperty("municipioUf").ValueKind == JsonValueKind.Null
            && d.GetProperty("uf").GetString() == "PA");
        ConferirDiaMunicipal(dias);
    }

    private static void ConferirDiaMunicipal(JsonElement dias)
    {
        dias.EnumerateArray().Should().Contain(d =>
            d.GetProperty("abrangencia").GetProperty("codigo").GetString() == "MUNICIPAL"
            && d.GetProperty("municipioIbge").GetString() == "1504208"
            && d.GetProperty("municipioNome").GetString() == "Marabá"
            && d.GetProperty("municipioUf").GetString() == "PA"
            && d.GetProperty("uf").ValueKind == JsonValueKind.Null);
    }

    private static object CorpoSemVersao() => new
    {
        diasNaoUteis = new[] { new { abrangencia = "NACIONAL", municipioIbge = (string?)null, municipioNome = (string?)null, municipioUf = (string?)null, data = "2027-01-01", descricao = "Confraternização Universal" } },
    };

    private static object DiaInvalido(string abrangencia, string data, string descricao) => new
    {
        abrangencia,
        municipioIbge = (string?)null,
        municipioNome = (string?)null,
        municipioUf = (string?)null,
        uf = (string?)null,
        data,
        descricao,
    };

    private static object CorpoValido(string? versaoDoCalendario = null) => new
    {
        versaoDoCalendario = versaoDoCalendario ?? "2027.1",
        diasNaoUteis = new object[]
        {
            new { abrangencia = "NACIONAL", municipioIbge = (string?)null, municipioNome = (string?)null, municipioUf = (string?)null, data = "2027-01-01", descricao = "Confraternização Universal" },
            new { abrangencia = "ESTADUAL", municipioIbge = (string?)null, municipioNome = (string?)null, municipioUf = (string?)null, uf = "PA", data = "2027-08-15", descricao = "Adesão do Pará à Independência" },
            new { abrangencia = "MUNICIPAL", municipioIbge = "1504208", municipioNome = "Marabá", municipioUf = "PA", data = "2027-05-08", descricao = "Aniversário de Marabá" },
        },
    };

    private static object CorpoComDia(string abrangencia, string? municipioIbge, string? municipioNome, string? municipioUf, string? uf = null) => new
    {
        versaoDoCalendario = VersaoUnica(),
        diasNaoUteis = new[] { new { abrangencia, municipioIbge, municipioNome, municipioUf, uf, data = "2027-05-08", descricao = "Dia não útil" } },
    };

    // Versão de até 60 chars, única por teste — não é chave natural, mas a
    // unicidade evita confundir calendários entre testes que rodam na mesma coleção.
    private static string VersaoUnica() => $"cal-{Guid.NewGuid():N}"[..20];

    private static async Task<Guid> CriarCalendario(HttpClient client, string versaoDoCalendario)
    {
        HttpResponseMessage criar = await EnviarPostAdmin(client, AdminPath, CorpoValido(versaoDoCalendario));
        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        return await criar.Content.ReadFromJsonAsync<Guid>();
    }

    private static async Task<bool> ObterVigente(HttpClient client, Guid id)
    {
        HttpResponseMessage obter = await client.GetAsync(new Uri($"{ColecaoPath}/{id}", UriKind.Relative));
        using JsonDocument doc = JsonDocument.Parse(await obter.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("vigente").GetBoolean();
    }

    private static async Task<string?> LerCodigoDeErro(HttpResponseMessage response)
    {
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("code").GetString();
    }

    private static async Task<HttpResponseMessage> EnviarPostAdmin(HttpClient client, string path, object? body)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri(path, UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> EnviarDeleteAdmin(HttpClient client, Guid id)
    {
        using HttpRequestMessage request = new(
            HttpMethod.Delete, new Uri($"{AdminPath}/{id}", UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
        return await client.SendAsync(request);
    }
}
