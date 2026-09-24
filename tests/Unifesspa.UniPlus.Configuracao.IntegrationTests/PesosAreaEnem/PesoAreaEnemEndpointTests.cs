namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.PesosAreaEnem;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

/// <summary>
/// Smoke + caminho de escrita dos endpoints de <c>PesoAreaEnem</c>: routing, vendor
/// media type, HATEOAS, autenticação/autorização, idempotência, as cinco áreas com código
/// e rótulo oficial postos pelo sistema e as recusas por campo, com Wolverine rodando
/// contra Postgres efêmero.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class PesoAreaEnemEndpointTests
{
    private const string VendorMime = "application/vnd.uniplus.peso-area-enem.v1+json";
    private const string VendorMimeAreas = "application/vnd.uniplus.area-peso-area-enem.v1+json";
    private const string ColecaoPath = "/api/configuracao/pesos-area-enem";
    private const string AreasPath = "/api/configuracao/pesos-area-enem/areas";
    private const string AdminPath = "/api/configuracao/admin/pesos-area-enem";
    private const string BaseLegal = "Res. 805/2024 Anexo I";

    private static readonly (string Codigo, string Rotulo)[] AreasEsperadas =
    [
        ("REDACAO", "Redação"),
        ("CIENCIAS_DA_NATUREZA", "Ciências da Natureza e suas Tecnologias"),
        ("CIENCIAS_HUMANAS", "Ciências Humanas e suas Tecnologias"),
        ("LINGUAGENS", "Linguagens e suas Tecnologias"),
        ("MATEMATICA", "Matemática e suas Tecnologias"),
    ];

    private readonly ConfiguracaoEndpointFixture _fixture;

    public PesoAreaEnemEndpointTests(ConfiguracaoEndpointFixture fixture)
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

    [Fact(DisplayName = "GET áreas devolve as cinco áreas com código e rótulo oficial, na ordem canônica, sem autenticação")]
    public async Task ListarAreas_DevolveAsCincoNaOrdem()
    {
        using HttpClient client = _fixture.Factory.CreateDefaultClient();

        HttpResponseMessage response = await client.GetAsync(new Uri(AreasPath, UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be(VendorMimeAreas);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.EnumerateArray()
            .Select(a => (a.GetProperty("codigo").GetString()!, a.GetProperty("rotulo").GetString()!))
            .Should().Equal(AreasEsperadas);
    }

    [Fact(DisplayName = "GET por id retorna 404 quando inexistente")]
    public async Task ObterPorId_NaoExiste_Retorna404()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri($"{ColecaoPath}/{Guid.NewGuid()}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
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

    [Fact(DisplayName = "POST admin autenticado sem role plataforma-admin retorna 403")]
    public async Task Criar_SemRoleAdmin_Retorna403()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri(AdminPath, UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "candidato");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(CorpoValido(ResolucaoUnica()));

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "POST admin cria (201) e o GET devolve as áreas com código, rótulo, peso e corte + _links")]
    public async Task Criar_ComAuthEIdempotency_Retorna201EPersiste()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarAdmin(client, HttpMethod.Post, AdminPath, CorpoValido(ResolucaoUnica()));

        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();
        id.Should().NotBe(Guid.Empty);

        using JsonDocument doc = await ObterAsync(client, id);
        JsonElement root = doc.RootElement;
        root.GetProperty("grupoCurso").GetProperty("codigo").GetString().Should().Be(GrupoCurso.Tecnologica);
        root.GetProperty("grupoCurso").GetProperty("rotulo").GetString().Should().Be("Tecnológica");
        root.GetProperty("baseLegal").GetString().Should().Be(BaseLegal);
        root.TryGetProperty("pesoRedacao", out _).Should().BeFalse("os cinco pesos fixos saíram do contrato");
        root.TryGetProperty("corteRedacao", out _).Should().BeFalse("o corte passou a ser de cada área");
        root.TryGetProperty("_links", out _).Should().BeTrue("HATEOAS Level 1 expõe _links.self (ADR-0029)");

        JsonElement[] areas = [.. root.GetProperty("areas").EnumerateArray()];
        areas.Select(a => (a.GetProperty("codigo").GetString()!, a.GetProperty("rotulo").GetString()!))
            .Should().Equal(AreasEsperadas);
        areas.Select(a => a.GetProperty("peso").GetDecimal()).Should().Equal(2.00m, 1.50m, 2.50m, 2.50m, 1.50m);
        areas[0].GetProperty("corte").GetDecimal().Should().Be(400m);
        areas.Skip(1).Should().OnlyContain(a => a.GetProperty("corte").ValueKind == JsonValueKind.Null);
    }

    [Fact(DisplayName = "POST admin grava o rótulo do sistema mesmo com as áreas em outra ordem")]
    public async Task Criar_AreasForaDeOrdem_DevolveNaOrdemCanonica()
    {
        object[] areas = [.. Enumerable.Reverse(AreasValidas())];

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarAdmin(client, HttpMethod.Post, AdminPath, Corpo(ResolucaoUnica(), areas));
        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();

        using JsonDocument doc = await ObterAsync(client, id);
        doc.RootElement.GetProperty("areas").EnumerateArray()
            .Select(a => a.GetProperty("codigo").GetString()!)
            .Should().Equal(AreasEsperadas.Select(a => a.Codigo));
    }

    [Fact(DisplayName = "PUT admin atualiza peso e corte de cada área e mantém código e rótulo")]
    public async Task Atualizar_MudaValores()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage criar = await EnviarAdmin(client, HttpMethod.Post, AdminPath, CorpoValido(ResolucaoUnica()));
        Guid id = await criar.Content.ReadFromJsonAsync<Guid>();

        object[] novas = AreasValidas();
        novas[0] = new { codigo = "REDACAO", peso = 3.00m };
        novas[4] = new { codigo = "MATEMATICA", peso = 4.25m };
        HttpResponseMessage atualizar = await EnviarAdmin(
            client, HttpMethod.Put, $"{AdminPath}/{id}", new { id, areas = novas, baseLegal = "Nova base" });

        atualizar.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using JsonDocument doc = await ObterAsync(client, id);
        JsonElement[] areas = [.. doc.RootElement.GetProperty("areas").EnumerateArray()];
        areas[0].GetProperty("peso").GetDecimal().Should().Be(3.00m);
        areas[0].GetProperty("corte").ValueKind.Should().Be(JsonValueKind.Null, "corte ausente no PUT é sem corte");
        areas[4].GetProperty("peso").GetDecimal().Should().Be(4.25m);
        areas.Select(a => (a.GetProperty("codigo").GetString()!, a.GetProperty("rotulo").GetString()!))
            .Should().Equal(AreasEsperadas);
        doc.RootElement.GetProperty("baseLegal").GetString().Should().Be("Nova base");
    }

    [Fact(DisplayName = "POST admin com peso acima do teto retorna 422 no campo do peso da área")]
    public async Task Criar_PesoAcimaDoMaximo_Retorna422NoCampo()
    {
        object[] areas = AreasValidas();
        areas[0] = new { codigo = "REDACAO", peso = 100.00m, corte = 400m };

        await AssertRecusaNoCampo(Corpo(ResolucaoUnica(), areas), "areas[0].peso",
            "uniplus.configuracao.peso_area_enem.peso_excede_maximo");
    }

    [Fact(DisplayName = "POST admin com corte acima do máximo retorna 422 no campo do corte da área")]
    public async Task Criar_CorteAcimaDoMaximo_Retorna422NoCampo()
    {
        object[] areas = AreasValidas();
        areas[0] = new { codigo = "REDACAO", peso = 2.00m, corte = 1000.001m };

        await AssertRecusaNoCampo(Corpo(ResolucaoUnica(), areas), "areas[0].corte",
            "uniplus.configuracao.peso_area_enem.corte_excede_maximo");
    }

    [Fact(DisplayName = "POST admin com corte em área que não é a Redação cria a linha com o corte")]
    public async Task Criar_CorteForaDaRedacao_Cria()
    {
        object[] areas = AreasValidas();
        areas[3] = new { codigo = "LINGUAGENS", peso = 2.50m, corte = 450m };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarAdmin(client, HttpMethod.Post, AdminPath, Corpo(ResolucaoUnica(), areas));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact(DisplayName = "POST admin com área fora das cinco retorna 422 no campo do código")]
    public async Task Criar_AreaForaDoDominio_Retorna422NoCampo()
    {
        object[] areas = AreasValidas();
        areas[2] = new { codigo = "FISICA", peso = 1.00m };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarAdmin(client, HttpMethod.Post, AdminPath, Corpo(ResolucaoUnica(), areas));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("errors").EnumerateArray()
            .Select(e => (e.GetProperty("field").GetString(), e.GetProperty("code").GetString()))
            .Should().BeEquivalentTo(
            [
                ("areas[2].codigo", "uniplus.configuracao.peso_area_enem.area_fora_do_dominio"),
                ("areas", "uniplus.configuracao.peso_area_enem.area_faltando"),
            ]);
    }

    [Fact(DisplayName = "POST admin com área repetida retorna 422 no campo do código repetido")]
    public async Task Criar_AreaRepetida_Retorna422NoCampo()
    {
        object[] areas = AreasValidas();
        areas[4] = new { codigo = "REDACAO", peso = 1.00m };

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarAdmin(client, HttpMethod.Post, AdminPath, Corpo(ResolucaoUnica(), areas));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("errors").EnumerateArray()
            .Should().Contain(e => e.GetProperty("field").GetString() == "areas[4].codigo"
                && e.GetProperty("code").GetString() == "uniplus.configuracao.peso_area_enem.area_repetida");
    }

    [Fact(DisplayName = "POST admin sem áreas retorna 422 no campo das áreas")]
    public async Task Criar_SemAreas_Retorna422()
    {
        var body = new { resolucao = ResolucaoUnica(), grupoCurso = GrupoCurso.Tecnologica, baseLegal = BaseLegal };

        await AssertRecusaNoCampo(body, "areas", "uniplus.configuracao.peso_area_enem.area_faltando");
    }

    [Theory(DisplayName = "POST admin com grupo fora dos quatro códigos retorna 422 no campo do grupo, inclusive o rótulo")]
    [InlineData("Engenharias")]
    [InlineData("Tecnológica")]
    public async Task Criar_GrupoInvalido_Retorna422(string grupoCurso)
    {
        var body = new { resolucao = ResolucaoUnica(), grupoCurso, areas = AreasValidas(), baseLegal = BaseLegal };

        await AssertRecusaNoCampo(body, "grupoCurso", "uniplus.configuracao.peso_area_enem.grupo_curso_invalido");
    }

    private static object[] AreasValidas() =>
    [
        new { codigo = "REDACAO", peso = 2.00m, corte = 400m },
        new { codigo = "CIENCIAS_DA_NATUREZA", peso = 1.50m },
        new { codigo = "CIENCIAS_HUMANAS", peso = 2.50m },
        new { codigo = "LINGUAGENS", peso = 2.50m },
        new { codigo = "MATEMATICA", peso = 1.50m },
    ];

    private static object CorpoValido(string resolucao) => Corpo(resolucao, AreasValidas());

    private static object Corpo(string resolucao, object[] areas) => new
    {
        resolucao,
        grupoCurso = GrupoCurso.Tecnologica,
        areas,
        baseLegal = BaseLegal,
    };

    // Resolução de até 40 chars, única por teste para não colidir na UNIQUE parcial do par.
    private static string ResolucaoUnica() => $"Res. {Guid.NewGuid().ToString("N")[..12]}";

    private async Task AssertRecusaNoCampo(object body, string campo, string codigo)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await EnviarAdmin(client, HttpMethod.Post, AdminPath, body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "o código precisa estar registrado em ConfiguracaoDomainErrorRegistration — sem isso o " +
            "mapeador cai no fallback uniplus.erro_nao_mapeado (400)");
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement erro = doc.RootElement.GetProperty("errors").EnumerateArray().Single();
        erro.GetProperty("field").GetString().Should().Be(campo);
        erro.GetProperty("code").GetString().Should().Be(codigo);
    }

    private static async Task<JsonDocument> ObterAsync(HttpClient client, Guid id)
    {
        HttpResponseMessage obter = await client.GetAsync(new Uri($"{ColecaoPath}/{id}", UriKind.Relative));
        obter.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonDocument.Parse(await obter.Content.ReadAsStringAsync());
    }

    private static async Task<HttpResponseMessage> EnviarAdmin(HttpClient client, HttpMethod metodo, string path, object body)
    {
        using HttpRequestMessage request = new(metodo, new Uri(path, UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }
}
