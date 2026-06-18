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
/// Endpoints públicos de leitura de Estados (#675) contra a API real + PostGIS.
/// Read-only <c>[AllowAnonymous]</c> — sem autenticação. Cobre CA-01, CA-07,
/// CA-09 e CA-10. A collection é serial; cada teste TRUNCA e semeia.
/// </summary>
[Collection(GeoPostgisCollection.Name)]
public sealed class EstadosEndpointTests
{
    private readonly GeoPostgisFixture _fixture;

    public EstadosEndpointTests(GeoPostgisFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "CA-01/CA-10: lista de estados pagina por cursor estável e cada item tem _links.self")]
    public async Task Estados_Lista_Paginada()
    {
        IReadOnlyList<string> ufsSemeadas = ["AC", "AL", "AM", "BA", "CE"];
        await SemearEstadosAsync(ufsSemeadas);

        using HttpClient client = _fixture.Factory.CreateClient();

        // Página 1 (limit=2) — espera-se header Link com next e _links.self por item.
        using HttpResponseMessage pagina1 = await GeoReferenceSeed.Obter(client,"/api/estados?limit=2");
        pagina1.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonElement itens1 = await LerArrayAsync(pagina1);
        itens1.GetArrayLength().Should().Be(2);
        foreach (JsonElement item in itens1.EnumerateArray())
        {
            string uf = item.GetProperty("uf").GetString()!;
            item.GetProperty("_links").GetProperty("self").GetString()
                .Should().Be($"/api/estados/{uf}");
        }

        GeoReferenceSeed.ExtrairLink(pagina1, "next").Should().NotBeNull("há mais páginas");

        // Percorre todas as páginas seguindo o cursor 'next': união = conjunto semeado,
        // sem duplicatas (keyset estável por Id — ADR-0089). Não assume ordem alfabética.
        List<string> coletadas = [];
        coletadas.AddRange(ExtrairUfs(itens1));

        string? proximo = GeoReferenceSeed.ExtrairLink(pagina1, "next");
        bool viuPrevEmPaginaSeguinte = false;
        while (proximo is not null)
        {
            using HttpResponseMessage pagina = await GeoReferenceSeed.Obter(client,proximo);
            pagina.StatusCode.Should().Be(HttpStatusCode.OK);
            JsonElement itens = await LerArrayAsync(pagina);
            coletadas.AddRange(ExtrairUfs(itens));

            if (GeoReferenceSeed.ExtrairLink(pagina, "prev") is not null)
            {
                viuPrevEmPaginaSeguinte = true;
            }

            proximo = GeoReferenceSeed.ExtrairLink(pagina, "next");
        }

        coletadas.Should().BeEquivalentTo(ufsSemeadas, "a paginação cobre todo o conjunto sem repetir/saltar");
        coletadas.Should().OnlyHaveUniqueItems();
        viuPrevEmPaginaSeguinte.Should().BeTrue("páginas após a primeira emitem rel=prev");
    }

    [Fact(DisplayName = "CA-09: detalhe do estado por UF traz núcleo + região + capital + coordenadas + _links")]
    public async Task Estado_Detalhe_PorUf()
    {
        await SemearParaAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await GeoReferenceSeed.Obter(client,"/api/estados/PA");
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        JsonElement raiz = doc.RootElement;
        raiz.GetProperty("uf").GetString().Should().Be("PA");
        raiz.GetProperty("nome").GetString().Should().Be("Pará");
        raiz.GetProperty("regiao").GetString().Should().Be("Norte");
        raiz.GetProperty("capital").GetString().Should().Be("Belém");
        raiz.GetProperty("latitude").GetDecimal().Should().Be(-3.79m);

        JsonElement links = raiz.GetProperty("_links");
        links.GetProperty("self").GetString().Should().Be("/api/estados/PA");
        links.GetProperty("cidades").GetString().Should().Be("/api/cidades?uf=PA");
        links.GetProperty("collection").GetString().Should().Be("/api/estados");
    }

    [Theory(DisplayName = "CA-09: UF case-insensitive resolve o mesmo estado")]
    [InlineData("pa")]
    [InlineData("Pa")]
    public async Task Estado_Detalhe_PorUf_CaseInsensitive(string uf)
    {
        await SemearParaAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await GeoReferenceSeed.Obter(client,$"/api/estados/{uf}");
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("uf").GetString().Should().Be("PA");
    }

    [Fact(DisplayName = "CA-09: UF bem-formada mas inexistente retorna 404")]
    public async Task Estado_Detalhe_Inexistente_404()
    {
        await SemearParaAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await GeoReferenceSeed.Obter(client,"/api/estados/ZZ");
        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory(DisplayName = "CA-09: UF malformada (≠2 letras) retorna 400")]
    [InlineData("/api/estados/X")]
    [InlineData("/api/estados/ABC")]
    [InlineData("/api/estados/12")]
    public async Task Estado_Detalhe_Malformada_400(string rota)
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await GeoReferenceSeed.Obter(client,rota);
        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "CA-07: Accept vendor incompatível retorna 406; vendor estado.v1 é aceito")]
    public async Task Estados_VendorMime()
    {
        await SemearParaAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpRequestMessage incompativel = new(HttpMethod.Get, "/api/estados");
        incompativel.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.uniplus.estado.v2+json"));
        using HttpResponseMessage resp406 = await client.SendAsync(incompativel);
        resp406.StatusCode.Should().Be(HttpStatusCode.NotAcceptable);

        using HttpRequestMessage compativel = new(HttpMethod.Get, "/api/estados");
        compativel.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.uniplus.estado.v1+json"));
        using HttpResponseMessage resp200 = await client.SendAsync(compativel);
        resp200.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "ADR-0092: estado stale (vigente=false) não aparece na lista nem no detalhe")]
    public async Task Estados_NaoVigente_NaoVaza()
    {
        await GeoReferenceSeed.LimparAsync(_fixture);
        await using (GeoDbContext ctx = _fixture.CreateDbContext())
        {
            Pais brasil = GeoReferenceSeed.NovoBrasil();
            Estado vigente = GeoReferenceSeed.NovoEstado(brasil.Id, "PA", "Pará");
            Estado stale = GeoReferenceSeed.NovoEstado(brasil.Id, "ZZ", "Obsoleto", vigente: false);
            ctx.Paises.Add(brasil);
            ctx.Estados.AddRange(vigente, stale);
            await ctx.SaveChangesAsync();
        }

        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage lista = await GeoReferenceSeed.Obter(client,"/api/estados?limit=100");
        JsonElement itens = await LerArrayAsync(lista);
        ExtrairUfs(itens).Should().Contain("PA").And.NotContain("ZZ");

        using HttpResponseMessage detalhe = await GeoReferenceSeed.Obter(client,"/api/estados/ZZ");
        detalhe.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task SemearEstadosAsync(IReadOnlyList<string> ufs)
    {
        await GeoReferenceSeed.LimparAsync(_fixture);
        await using GeoDbContext ctx = _fixture.CreateDbContext();
        Pais brasil = GeoReferenceSeed.NovoBrasil();
        ctx.Paises.Add(brasil);
        foreach (string uf in ufs)
        {
            ctx.Estados.Add(GeoReferenceSeed.NovoEstado(brasil.Id, uf, $"Estado {uf}"));
        }

        await ctx.SaveChangesAsync();
    }

    private async Task SemearParaAsync()
    {
        await GeoReferenceSeed.LimparAsync(_fixture);
        await using GeoDbContext ctx = _fixture.CreateDbContext();
        Pais brasil = GeoReferenceSeed.NovoBrasil();
        Estado para = GeoReferenceSeed.NovoEstado(
            brasil.Id, "PA", "Pará", regiao: "Norte", capital: "Belém", codigoIbge: "15",
            latitude: -3.79m, longitude: -52.48m);
        ctx.Paises.Add(brasil);
        ctx.Estados.Add(para);
        await ctx.SaveChangesAsync();
    }

    private static async Task<JsonElement> LerArrayAsync(HttpResponseMessage resposta)
    {
        using JsonDocument doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    private static List<string> ExtrairUfs(JsonElement array) =>
        [.. array.EnumerateArray().Select(item => item.GetProperty("uf").GetString()!)];
}
