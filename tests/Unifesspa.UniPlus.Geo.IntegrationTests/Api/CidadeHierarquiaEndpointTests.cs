namespace Unifesspa.UniPlus.Geo.IntegrationTests.Api;

using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Geo.Domain.Entities;
using Unifesspa.UniPlus.Geo.Infrastructure.Persistence;
using Unifesspa.UniPlus.Geo.IntegrationTests.Infrastructure;

/// <summary>
/// Endpoints públicos de hierarquia/autocomplete Geo (#677) contra API real +
/// PostGIS. Cobre Cidade → Distrito/Bairro/Logradouro, cursor, HATEOAS e boundary.
/// </summary>
[Collection(GeoPostgisCollection.Name)]
public sealed class CidadeHierarquiaEndpointTests
{
    private readonly GeoPostgisFixture _fixture;

    public CidadeHierarquiaEndpointTests(GeoPostgisFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "CA-01: distritos de uma cidade são paginados e têm _links cidade+collection sem self")]
    public async Task Distritos_PorCidade_Paginado()
    {
        await SemearAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage pagina1 = await GeoReferenceSeed.Obter(client, "/api/cidades/3550308/distritos?limit=1");
        pagina1.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonElement itensPagina1 = await LerArrayAsync(pagina1);
        itensPagina1.GetArrayLength().Should().Be(1);
        JsonElement primeiro = itensPagina1.EnumerateArray().Single();
        primeiro.GetProperty("cidadeCodigoIbge").GetString().Should().Be("3550308");
        JsonElement links = primeiro.GetProperty("_links");
        links.GetProperty("cidade").GetString().Should().Be("/api/cidades/3550308");
        links.GetProperty("collection").GetString().Should().Be("/api/cidades/3550308/distritos");
        links.TryGetProperty("self", out _).Should().BeFalse();
        primeiro.GetProperty("latitude").ValueKind.Should().Be(JsonValueKind.Number);
        primeiro.GetProperty("longitude").ValueKind.Should().Be(JsonValueKind.Number);

        string? proximo = GeoReferenceSeed.ExtrairLink(pagina1, "next");
        proximo.Should().NotBeNull();
        proximo.Should().Contain("/api/cidades/3550308/distritos");

        List<string> nomes = [.. LerNomes(itensPagina1)];
        using HttpResponseMessage pagina2 = await GeoReferenceSeed.Obter(client, proximo!);
        pagina2.StatusCode.Should().Be(HttpStatusCode.OK);
        nomes.AddRange(LerNomes(await LerArrayAsync(pagina2)));

        nomes.Should().BeEquivalentTo(["Sé", "Pinheiros"]);
    }

    [Theory(DisplayName = "CA-02: busca de bairro é acento e caixa-insensível")]
    [InlineData("sa")]
    [InlineData("SÁ")]
    public async Task Bairros_Busca_AcentoInsensivel(string termo)
    {
        await SemearAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await GeoReferenceSeed.Obter(
            client, $"/api/cidades/3550308/bairros?q={Uri.EscapeDataString(termo)}&limit=100");
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonElement itens = await LerArrayAsync(resposta);
        List<string> nomes = LerNomes(itens);
        nomes.Should().BeEquivalentTo(["Santa Cecília", "Saúde"]);
        nomes.Should().NotContain("Sé");

        JsonElement links = itens.EnumerateArray().First().GetProperty("_links");
        links.GetProperty("cidade").GetString().Should().Be("/api/cidades/3550308");
        links.GetProperty("collection").GetString().Should().Be("/api/cidades/3550308/bairros");
        links.TryGetProperty("self", out _).Should().BeFalse();
        itens.EnumerateArray()
            .Single(b => b.GetProperty("nome").GetString() == "Saúde")
            .GetProperty("latitude").GetDecimal().Should().Be(-23.61811m);
    }

    [Fact(DisplayName = "CA-03: autocomplete de logradouro busca por nome (acento-insensível), escopado à cidade, com _links.cep")]
    public async Task Logradouros_Autocomplete_PorCidade()
    {
        await SemearAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        // A busca corre sobre NomeNormalizado (= nome sem o tipo, como o ETL grava),
        // então casa pelo NOME do logradouro acento/caixa-insensível: "sé" → "Sé".
        using HttpResponseMessage resposta = await GeoReferenceSeed.Obter(
            client, "/api/cidades/3550308/logradouros?q=s%C3%A9&limit=100");
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonElement itens = await LerArrayAsync(resposta);
        itens.GetArrayLength().Should().Be(1);
        JsonElement item = itens.EnumerateArray().Single();
        item.GetProperty("cep").GetString().Should().Be("01001000");
        item.GetProperty("tipo").GetString().Should().Be("Praça");
        item.GetProperty("nome").GetString().Should().Be("Sé");
        item.GetProperty("nomeCompleto").GetString().Should().Be("Praça da Sé");
        item.GetProperty("bairro").GetString().Should().Be("Sé");
        item.GetProperty("cidadeCodigoIbge").GetString().Should().Be("3550308");
        item.GetProperty("uf").GetString().Should().Be("SP");

        JsonElement links = item.GetProperty("_links");
        links.GetProperty("cidade").GetString().Should().Be("/api/cidades/3550308");
        links.GetProperty("cep").GetString().Should().Be("/api/cep/01001000");
        links.GetProperty("collection").GetString().Should().Be("/api/cidades/3550308/logradouros");
        links.TryGetProperty("self", out _).Should().BeFalse();

        // Limitação conhecida (rastreada em #707): a busca não casa o TIPO ("Praça"),
        // que vive em coluna separada e não compõe NomeNormalizado. Quando #707 entregar
        // a busca por texto completo + ranking por similaridade, este assert deve mudar.
        using HttpResponseMessage porTipo = await GeoReferenceSeed.Obter(
            client, "/api/cidades/3550308/logradouros?q=pra%C3%A7a&limit=100");
        porTipo.StatusCode.Should().Be(HttpStatusCode.OK);
        (await LerArrayAsync(porTipo)).GetArrayLength().Should().Be(0);
    }

    [Fact(DisplayName = "CA-04: cidade inexistente retorna 404; cidade existente sem filhos do filtro retorna 200 vazio")]
    public async Task Hierarquia_CidadeInexistente_404_EFiltroVazio_200()
    {
        await SemearAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage inexistente = await GeoReferenceSeed.Obter(client, "/api/cidades/9999999/distritos");
        inexistente.StatusCode.Should().Be(HttpStatusCode.NotFound);

        using HttpResponseMessage vazio = await GeoReferenceSeed.Obter(client, "/api/cidades/3550308/bairros?q=zzzzz");
        vazio.StatusCode.Should().Be(HttpStatusCode.OK);
        (await LerArrayAsync(vazio)).GetArrayLength().Should().Be(0);
    }

    [Theory(DisplayName = "CA-05: código IBGE malformado retorna 400")]
    [InlineData("/api/cidades/abc/distritos")]
    [InlineData("/api/cidades/123/bairros")]
    [InlineData("/api/cidades/35503080/logradouros")]
    public async Task Hierarquia_CodigoIbgeInvalido_400(string rota)
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await GeoReferenceSeed.Obter(client, rota);
        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "CA-06: cursor preserva escopo da cidade e q no header Link")]
    public async Task Hierarquia_Cursor_PreservaEscopoEFiltro()
    {
        await SemearAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage pagina1 = await GeoReferenceSeed.Obter(client, "/api/cidades/3550308/bairros?q=sa&limit=1");
        pagina1.StatusCode.Should().Be(HttpStatusCode.OK);

        string? proximo = GeoReferenceSeed.ExtrairLink(pagina1, "next");
        proximo.Should().NotBeNull();
        proximo.Should().Contain("/api/cidades/3550308/bairros").And.Contain("q=sa");

        List<string> primeiraPagina = LerNomes(await LerArrayAsync(pagina1));
        using HttpResponseMessage pagina2 = await GeoReferenceSeed.Obter(client, proximo!);
        pagina2.StatusCode.Should().Be(HttpStatusCode.OK);

        string? anterior = GeoReferenceSeed.ExtrairLink(pagina2, "prev");
        anterior.Should().NotBeNull();
        anterior.Should().Contain("/api/cidades/3550308/bairros").And.Contain("q=sa");

        using HttpResponseMessage volta = await GeoReferenceSeed.Obter(client, anterior!);
        volta.StatusCode.Should().Be(HttpStatusCode.OK);
        LerNomes(await LerArrayAsync(volta)).Should().Equal(primeiraPagina);

        List<string> nomes = [.. primeiraPagina, .. LerNomes(await LerArrayAsync(pagina2))];
        proximo = GeoReferenceSeed.ExtrairLink(pagina2, "next");
        while (proximo is not null)
        {
            using HttpResponseMessage pagina = await GeoReferenceSeed.Obter(client, proximo);
            pagina.StatusCode.Should().Be(HttpStatusCode.OK);
            nomes.AddRange(LerNomes(await LerArrayAsync(pagina)));
            proximo = GeoReferenceSeed.ExtrairLink(pagina, "next");
        }

        nomes.Should().BeEquivalentTo(["Santa Cecília", "Saúde"]);
    }

    [Fact(DisplayName = "CA-07: Accept vendor incompatível retorna 406; termo q longo retorna 400")]
    public async Task Hierarquia_VendorMime_TermoLongo()
    {
        await SemearAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpRequestMessage incompativel = new(HttpMethod.Get, "/api/cidades/3550308/logradouros?q=praca");
        incompativel.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.uniplus.logradouro.v2+json"));
        using HttpResponseMessage resp406 = await client.SendAsync(incompativel);
        resp406.StatusCode.Should().Be(HttpStatusCode.NotAcceptable);

        using HttpRequestMessage compativel = new(HttpMethod.Get, "/api/cidades/3550308/logradouros?q=praca");
        compativel.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.uniplus.logradouro.v1+json"));
        using HttpResponseMessage resp200 = await client.SendAsync(compativel);
        resp200.StatusCode.Should().Be(HttpStatusCode.OK);

        string termoLongo = new('a', 257);
        using HttpResponseMessage longo = await GeoReferenceSeed.Obter(
            client, $"/api/cidades/3550308/logradouros?q={termoLongo}");
        longo.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task SemearAsync()
    {
        await GeoReferenceSeed.LimparAsync(_fixture);
        await using GeoDbContext ctx = _fixture.CreateDbContext();

        Pais brasil = GeoReferenceSeed.NovoBrasil();
        Estado saoPaulo = GeoReferenceSeed.NovoEstado(brasil.Id, "SP", "São Paulo");
        Estado para = GeoReferenceSeed.NovoEstado(brasil.Id, "PA", "Pará");

        Cidade cidadeSp = GeoReferenceSeed.NovaCidade(saoPaulo.Id, "SP", "3550308", "São Paulo");
        Cidade maraba = GeoReferenceSeed.NovaCidade(para.Id, "PA", "1500402", "Marabá");

        Distrito seDistrito = GeoReferenceSeed.NovoDistrito(
            cidadeSp.Id, "SP", "Sé", latitude: -23.55052m, longitude: -46.63331m);
        Distrito pinheirosDistrito = GeoReferenceSeed.NovoDistrito(
            cidadeSp.Id, "SP", "Pinheiros", latitude: -23.56723m, longitude: -46.70103m);
        Distrito novaMaraba = GeoReferenceSeed.NovoDistrito(maraba.Id, "PA", "Nova Marabá");

        Bairro se = GeoReferenceSeed.NovoBairro(cidadeSp.Id, "SP", "Sé");
        Bairro santaCecilia = GeoReferenceSeed.NovoBairro(cidadeSp.Id, "SP", "Santa Cecília");
        Bairro saude = GeoReferenceSeed.NovoBairro(
            cidadeSp.Id, "SP", "Saúde", latitude: -23.61811m, longitude: -46.63878m);
        Bairro cidadeNova = GeoReferenceSeed.NovoBairro(maraba.Id, "PA", "Cidade Nova");

        // Espelha o ETL real (#673): nome_logradouro NÃO inclui o tipo; o texto cheio
        // (com o tipo) vive em nome_completo. A busca atual roda sobre NomeNormalizado
        // (= nome sem tipo), então casa por NOME — busca por tipo/texto-completo é #707.
        Logradouro pracaSe = GeoReferenceSeed.NovoLogradouro(
            cidadeSp.Id, "SP", "01001000", "Sé", tipo: "Praça", nomeCompleto: "Praça da Sé", bairroId: se.Id);
        Logradouro ruaSantaCecilia = GeoReferenceSeed.NovoLogradouro(
            cidadeSp.Id, "SP", "01225000", "Santa Cecília", tipo: "Rua", nomeCompleto: "Rua Santa Cecília", bairroId: santaCecilia.Id);
        Logradouro pracaMaraba = GeoReferenceSeed.NovoLogradouro(
            maraba.Id, "PA", "68500010", "São Félix", tipo: "Praça", nomeCompleto: "Praça São Félix", bairroId: cidadeNova.Id);

        ctx.Paises.Add(brasil);
        ctx.Estados.AddRange(saoPaulo, para);
        ctx.Cidades.AddRange(cidadeSp, maraba);
        ctx.Distritos.AddRange(seDistrito, pinheirosDistrito, novaMaraba);
        ctx.Bairros.AddRange(se, santaCecilia, saude, cidadeNova);
        ctx.Logradouros.AddRange(pracaSe, ruaSantaCecilia, pracaMaraba);
        await ctx.SaveChangesAsync();
    }

    private static async Task<JsonElement> LerArrayAsync(HttpResponseMessage resposta)
    {
        using JsonDocument doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    private static List<string> LerNomes(JsonElement array) =>
        [.. array.EnumerateArray().Select(item => item.GetProperty("nome").GetString()!)];
}
