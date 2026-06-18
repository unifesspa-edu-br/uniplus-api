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
/// Endpoints públicos de leitura de Cidades (#675) contra a API real + PostGIS.
/// Read-only <c>[AllowAnonymous]</c>. Cobre CA-02 a CA-08. A collection é serial;
/// cada teste TRUNCA e semeia.
/// </summary>
[Collection(GeoPostgisCollection.Name)]
public sealed class CidadesEndpointTests
{
    private readonly GeoPostgisFixture _fixture;

    public CidadesEndpointTests(GeoPostgisFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "CA-02: filtro por UF retorna só cidades da UF; UF inexistente é lista vazia (200)")]
    public async Task Cidades_FiltraPorUf()
    {
        await SemearAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage soPara = await GeoReferenceSeed.Obter(client,"/api/cidades?uf=PA&limit=100");
        soPara.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement itensPa = await LerArrayAsync(soPara);
        itensPa.EnumerateArray().Select(c => c.GetProperty("uf").GetString())
            .Should().OnlyContain(uf => uf == "PA").And.HaveCountGreaterThan(1);

        using HttpResponseMessage inexistente = await GeoReferenceSeed.Obter(client,"/api/cidades?uf=ZZ&limit=100");
        inexistente.StatusCode.Should().Be(HttpStatusCode.OK);
        (await LerArrayAsync(inexistente)).GetArrayLength().Should().Be(0);
    }

    [Theory(DisplayName = "CA-03: busca por nome é acento e caixa-insensível")]
    [InlineData("maraba")]
    [InlineData("MARABÁ")]
    [InlineData("marabá")]
    public async Task Cidades_Busca_AcentoInsensivel(string termo)
    {
        await SemearAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await GeoReferenceSeed.Obter(client,$"/api/cidades?q={Uri.EscapeDataString(termo)}&limit=100");
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonElement itens = await LerArrayAsync(resposta);
        itens.EnumerateArray().Select(c => c.GetProperty("nome").GetString())
            .Should().Contain("Marabá");
    }

    [Fact(DisplayName = "CA-04: filtros uf+q são preservados no cursor (header Link de next)")]
    public async Task Cidades_FiltroMaisCursor_Persiste()
    {
        await SemearAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        // 'a' casa Marabá e Parauapebas (PA); Óbidos (PA, "obidos") NÃO casa.
        // limit=1 força paginação — exercita o filtro em cada página seguida.
        using HttpResponseMessage pagina1 = await GeoReferenceSeed.Obter(client,"/api/cidades?uf=PA&q=a&limit=1");
        pagina1.StatusCode.Should().Be(HttpStatusCode.OK);

        string? proximo = GeoReferenceSeed.ExtrairLink(pagina1, "next");
        proximo.Should().NotBeNull("há mais de uma cidade PA casando 'a'");
        proximo.Should().Contain("uf=PA").And.Contain("q=a");

        // Percorre TODAS as páginas seguindo o cursor: o conjunto coletado deve ser
        // exatamente {Marabá, Parauapebas} — Óbidos nunca aparece (prova que q segue
        // filtrando após o cursor, não só na primeira página).
        List<string> nomes = [.. LerNomes(await LerArrayAsync(pagina1))];
        while (proximo is not null)
        {
            using HttpResponseMessage pagina = await GeoReferenceSeed.Obter(client, proximo);
            pagina.StatusCode.Should().Be(HttpStatusCode.OK);
            nomes.AddRange(LerNomes(await LerArrayAsync(pagina)));
            proximo = GeoReferenceSeed.ExtrairLink(pagina, "next");
        }

        nomes.Should().BeEquivalentTo(["Marabá", "Parauapebas"]);
        nomes.Should().NotContain("Óbidos");
    }

    [Theory(DisplayName = "Segurança ILIKE: metacaractere de busca é tratado como literal (não casa tudo)")]
    [InlineData("%")]
    [InlineData("_")]
    public async Task Cidades_Busca_MetacaractereLiteral(string termo)
    {
        await SemearAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        // Sem escape, "%"/"_" casariam quase tudo. Com o escape do reader, são texto
        // literal: nenhum nome contém '%' ou '_', então o resultado é vazio.
        using HttpResponseMessage resposta = await GeoReferenceSeed.Obter(
            client, $"/api/cidades?q={Uri.EscapeDataString(termo)}&limit=100");
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        (await LerArrayAsync(resposta)).GetArrayLength().Should().Be(0);
    }

    [Fact(DisplayName = "CA-05: detalhe por código IBGE traz territorial + indicador 1:1 + _links")]
    public async Task Cidade_Detalhe_PorCodigoIbge()
    {
        await SemearAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await GeoReferenceSeed.Obter(client,"/api/cidades/1500402");
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        JsonElement raiz = doc.RootElement;
        raiz.GetProperty("codigoIbge").GetString().Should().Be("1500402");
        raiz.GetProperty("nome").GetString().Should().Be("Marabá");
        raiz.GetProperty("uf").GetString().Should().Be("PA");
        raiz.GetProperty("mesorregiaoNome").GetString().Should().Be("Sudeste Paraense");

        JsonElement indicador = raiz.GetProperty("indicador");
        indicador.GetProperty("gentilico").GetString().Should().Be("marabaense");
        indicador.GetProperty("idh").GetDecimal().Should().Be(0.668m);
        indicador.GetProperty("aniversario").GetString().Should().Be("05/04");

        JsonElement links = raiz.GetProperty("_links");
        links.GetProperty("self").GetString().Should().Be("/api/cidades/1500402");
        links.GetProperty("estado").GetString().Should().Be("/api/estados/PA");
        links.GetProperty("collection").GetString().Should().Be("/api/cidades");
    }

    [Fact(DisplayName = "CA-05: cidade sem indicador omite o campo indicador (null) sem erro")]
    public async Task Cidade_Detalhe_SemIndicador()
    {
        await GeoReferenceSeed.LimparAsync(_fixture);
        await using (GeoDbContext ctx = _fixture.CreateDbContext())
        {
            Pais brasil = GeoReferenceSeed.NovoBrasil();
            Estado para = GeoReferenceSeed.NovoEstado(brasil.Id, "PA", "Pará");
            Cidade semIndicador = GeoReferenceSeed.NovaCidade(para.Id, "PA", "1500701", "Abel Figueiredo");
            ctx.Paises.Add(brasil);
            ctx.Estados.Add(para);
            ctx.Cidades.Add(semIndicador);
            await ctx.SaveChangesAsync();
        }

        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpResponseMessage resposta = await GeoReferenceSeed.Obter(client,"/api/cidades/1500701");
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        doc.RootElement.TryGetProperty("indicador", out JsonElement indicador).Should().BeTrue();
        indicador.ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact(DisplayName = "CA-06: código IBGE bem-formado e inexistente retorna 404")]
    public async Task Cidade_Detalhe_Inexistente_404()
    {
        await SemearAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await GeoReferenceSeed.Obter(client,"/api/cidades/9999999");
        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory(DisplayName = "CA-06: código IBGE malformado (≠7 dígitos) retorna 400")]
    [InlineData("/api/cidades/abc")]
    [InlineData("/api/cidades/123")]
    [InlineData("/api/cidades/15004020")]
    public async Task Cidade_Detalhe_Malformado_400(string rota)
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await GeoReferenceSeed.Obter(client,rota);
        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "CA-07: Accept vendor incompatível retorna 406; vendor cidade.v1 é aceito")]
    public async Task Cidades_VendorMime_406()
    {
        await SemearAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpRequestMessage incompativel = new(HttpMethod.Get, "/api/cidades");
        incompativel.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.uniplus.cidade.v2+json"));
        using HttpResponseMessage resp406 = await client.SendAsync(incompativel);
        resp406.StatusCode.Should().Be(HttpStatusCode.NotAcceptable);

        using HttpRequestMessage compativel = new(HttpMethod.Get, "/api/cidades");
        compativel.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.uniplus.cidade.v1+json"));
        using HttpResponseMessage resp200 = await client.SendAsync(compativel);
        resp200.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "CA-08: termo de busca acima do limite retorna 400")]
    public async Task Cidades_Busca_Longa_400()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        string termoLongo = new('a', 257);
        using HttpResponseMessage resposta = await GeoReferenceSeed.Obter(client,$"/api/cidades?q={termoLongo}");
        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "Busca sobre nome_normalizado null: cidade não aparece na busca, mas aparece sem filtro")]
    public async Task Cidades_Busca_NomeNormalizadoNulo()
    {
        await GeoReferenceSeed.LimparAsync(_fixture);
        await using (GeoDbContext ctx = _fixture.CreateDbContext())
        {
            Pais brasil = GeoReferenceSeed.NovoBrasil();
            Estado para = GeoReferenceSeed.NovoEstado(brasil.Id, "PA", "Pará");
            // nomeNormalizado explicitamente vazio → normalizado para null pela entidade.
            Cidade semNorm = GeoReferenceSeed.NovaCidade(para.Id, "PA", "1500800", "Acará", nomeNormalizado: "");
            ctx.Paises.Add(brasil);
            ctx.Estados.Add(para);
            ctx.Cidades.Add(semNorm);
            await ctx.SaveChangesAsync();
        }

        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage busca = await GeoReferenceSeed.Obter(client,"/api/cidades?q=acara&limit=100");
        (await LerArrayAsync(busca)).GetArrayLength().Should().Be(0, "ILIKE sobre coluna NULL não casa");

        using HttpResponseMessage semFiltro = await GeoReferenceSeed.Obter(client,"/api/cidades?limit=100");
        (await LerArrayAsync(semFiltro)).EnumerateArray()
            .Select(c => c.GetProperty("codigoIbge").GetString())
            .Should().Contain("1500800", "sem filtro a cidade aparece normalmente");
    }

    [Fact(DisplayName = "ADR-0092: cidade stale (vigente=false) não aparece na lista nem no detalhe")]
    public async Task Cidades_NaoVigente_NaoVaza()
    {
        await GeoReferenceSeed.LimparAsync(_fixture);
        await using (GeoDbContext ctx = _fixture.CreateDbContext())
        {
            Pais brasil = GeoReferenceSeed.NovoBrasil();
            Estado para = GeoReferenceSeed.NovoEstado(brasil.Id, "PA", "Pará");
            Cidade vigente = GeoReferenceSeed.NovaCidade(para.Id, "PA", "1500402", "Marabá");
            Cidade stale = GeoReferenceSeed.NovaCidade(para.Id, "PA", "1599999", "Obsoleta", vigente: false);
            ctx.Paises.Add(brasil);
            ctx.Estados.Add(para);
            ctx.Cidades.AddRange(vigente, stale);
            await ctx.SaveChangesAsync();
        }

        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage lista = await GeoReferenceSeed.Obter(client,"/api/cidades?limit=100");
        (await LerArrayAsync(lista)).EnumerateArray()
            .Select(c => c.GetProperty("codigoIbge").GetString())
            .Should().Contain("1500402").And.NotContain("1599999");

        using HttpResponseMessage detalhe = await GeoReferenceSeed.Obter(client,"/api/cidades/1599999");
        detalhe.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task SemearAsync()
    {
        await GeoReferenceSeed.LimparAsync(_fixture);
        await using GeoDbContext ctx = _fixture.CreateDbContext();

        Pais brasil = GeoReferenceSeed.NovoBrasil();
        Estado para = GeoReferenceSeed.NovoEstado(brasil.Id, "PA", "Pará", regiao: "Norte", capital: "Belém");
        Estado saoPaulo = GeoReferenceSeed.NovoEstado(brasil.Id, "SP", "São Paulo", regiao: "Sudeste", capital: "São Paulo");

        Cidade maraba = GeoReferenceSeed.NovaCidade(
            para.Id, "PA", "1500402", "Marabá", nomeNormalizado: "maraba", ddd: "94",
            latitude: -5.36867m, longitude: -49.11731m,
            mesorregiaoNome: "Sudeste Paraense", microrregiaoNome: "Marabá",
            regiaoIntermediariaNome: "Marabá", regiaoImediataNome: "Marabá");
        Cidade parauapebas = GeoReferenceSeed.NovaCidade(
            para.Id, "PA", "1505536", "Parauapebas", nomeNormalizado: "parauapebas", ddd: "94");
        // Óbidos (PA) NÃO contém "a" no nome normalizado — prova negativa da busca q=a.
        Cidade obidos = GeoReferenceSeed.NovaCidade(
            para.Id, "PA", "1505106", "Óbidos", nomeNormalizado: "obidos", ddd: "93");
        Cidade saoPauloCidade = GeoReferenceSeed.NovaCidade(
            saoPaulo.Id, "SP", "3550308", "São Paulo", nomeNormalizado: "sao paulo", ddd: "11");

        CidadeIndicador indicadorMaraba = GeoReferenceSeed.NovoIndicador(
            maraba.Id, gentilico: "marabaense", areaKm2: 15128.058m, populacaoResidente: 233669,
            densidadeDemografica: 15.45m, idh: 0.668m, aniversario: "05/04");

        ctx.Paises.Add(brasil);
        ctx.Estados.AddRange(para, saoPaulo);
        ctx.Cidades.AddRange(maraba, parauapebas, obidos, saoPauloCidade);
        ctx.CidadeIndicadores.Add(indicadorMaraba);
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
