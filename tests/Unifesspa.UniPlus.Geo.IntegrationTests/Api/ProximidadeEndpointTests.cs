namespace Unifesspa.UniPlus.Geo.IntegrationTests.Api;

using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

using AwesomeAssertions;

using NetTopologySuite.Geometries;

using Unifesspa.UniPlus.Geo.Domain.Entities;
using Unifesspa.UniPlus.Geo.Infrastructure.Persistence;
using Unifesspa.UniPlus.Geo.IntegrationTests.Infrastructure;

/// <summary>
/// Consulta de proximidade geoespacial (#678) contra a API real + PostGIS. Read-only
/// <c>[AllowAnonymous]</c>. Ponto de referência fixo próximo a Marabá
/// (<c>lat=-5.35</c>, <c>long=-49.13</c>). A collection é serial; cada teste TRUNCA e
/// semeia cidades/logradouros com coordenada (<c>geography(Point,4326)</c>).
/// </summary>
[Collection(GeoPostgisCollection.Name)]
public sealed class ProximidadeEndpointTests
{
    private readonly GeoPostgisFixture _fixture;

    public ProximidadeEndpointTests(GeoPostgisFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "CA-01: cidades dentro do raio, ordenadas por distância, com DistanciaKm e _links")]
    public async Task CidadesProximas_OrdenaPorDistancia()
    {
        await SemearAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await GeoReferenceSeed.Obter(
            client, "/api/cidades/proximas?lat=-5.35&long=-49.13&raioKm=100");
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        JsonElement raiz = doc.RootElement;
        raiz.ValueKind.Should().Be(JsonValueKind.Array);

        List<string> nomes = [.. raiz.EnumerateArray().Select(c => c.GetProperty("nome").GetString()!)];
        nomes.Should().Equal("Marabá", "Eldorado dos Carajás");
        nomes.Should().NotContain("São Paulo");                  // > 100 km
        nomes.Should().NotContain("Bom Jesus do Tocantins");     // sem coordenada

        JsonElement primeiro = raiz[0];
        primeiro.GetProperty("distanciaKm").GetDouble().Should().BeGreaterThan(0);
        primeiro.GetProperty("_links").GetProperty("cidade").GetString().Should().Be("/api/cidades/1500402");

        double d0 = raiz[0].GetProperty("distanciaKm").GetDouble();
        double d1 = raiz[1].GetProperty("distanciaKm").GetDouble();
        d0.Should().BeLessThan(d1, "a lista é ordenada por distância crescente");
    }

    [Fact(DisplayName = "CA-03: logradouros dentro do raio, ordenados por distância, com cidade e _links.cep")]
    public async Task LogradourosProximos_PorPonto()
    {
        await SemearAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await GeoReferenceSeed.Obter(
            client, "/api/logradouros/proximos?lat=-5.35&long=-49.13&raioKm=50");
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        JsonElement raiz = doc.RootElement;

        List<string> nomes = [.. raiz.EnumerateArray().Select(l => l.GetProperty("nome").GetString()!)];
        nomes.Should().Equal("A", "B");          // Rua A (~1 km) antes de Avenida B (~9 km)
        nomes.Should().NotContain("da Sé");      // São Paulo, longe

        JsonElement primeiro = raiz[0];
        primeiro.GetProperty("cidadeNome").GetString().Should().Be("Marabá");
        primeiro.GetProperty("cidadeCodigoIbge").GetString().Should().Be("1500402");
        primeiro.GetProperty("distanciaKm").GetDouble().Should().BeGreaterThan(0);

        JsonElement links = primeiro.GetProperty("_links");
        links.GetProperty("cidade").GetString().Should().Be("/api/cidades/1500402");
        links.GetProperty("cep").GetString().Should().Be("/api/cep/68500000");
    }

    [Theory(DisplayName = "CA-04: parâmetro ausente ou fora de faixa retorna 400")]
    [InlineData("/api/cidades/proximas?long=-49.13&raioKm=100")]            // sem lat
    [InlineData("/api/cidades/proximas?lat=-5.35&raioKm=100")]              // sem long
    [InlineData("/api/cidades/proximas?lat=-5.35&long=-49.13")]            // sem raioKm
    [InlineData("/api/cidades/proximas?lat=200&long=-49.13&raioKm=10")]    // lat fora de faixa
    [InlineData("/api/cidades/proximas?lat=-5.35&long=-200&raioKm=10")]    // long fora de faixa
    [InlineData("/api/cidades/proximas?lat=-5.35&long=-49.13&raioKm=0")]   // raio = 0
    [InlineData("/api/cidades/proximas?lat=-5.35&long=-49.13&raioKm=9999")] // raio > teto
    [InlineData("/api/cidades/proximas?lat=-5.35&long=-49.13&raioKm=10&limit=0")] // limit <= 0
    public async Task CidadesProximas_Validacao_400(string rota)
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await GeoReferenceSeed.Obter(client, rota);
        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "CA-05: registro sem coordenada (ou sem lat/long) não aparece no resultado")]
    public async Task CidadesProximas_SemCoordenada_Exclui()
    {
        await SemearAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await GeoReferenceSeed.Obter(
            client, "/api/cidades/proximas?lat=-5.35&long=-49.13&raioKm=100");
        using JsonDocument doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());

        List<string> nomes = [.. doc.RootElement.EnumerateArray().Select(c => c.GetProperty("nome").GetString()!)];
        nomes.Should().NotContain("Bom Jesus do Tocantins", "Coordenada null não entra (ST_DWithin sobre NULL é falso)");
        nomes.Should().NotContain("Brejo Grande do Araguaia", "Coordenada presente mas lat/long nulos também é excluída");
    }

    [Fact(DisplayName = "CA-06: ponto no oceano com raio pequeno retorna 200 com lista vazia")]
    public async Task CidadesProximas_RaioVazio_ListaVazia()
    {
        await SemearAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await GeoReferenceSeed.Obter(
            client, "/api/cidades/proximas?lat=0&long=-30&raioKm=10");
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        doc.RootElement.GetArrayLength().Should().Be(0);
    }

    [Fact(DisplayName = "CA-06: limit=1 devolve apenas a cidade mais próxima (top-N por distância)")]
    public async Task CidadesProximas_Limit_RespeitaTopN()
    {
        await SemearAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await GeoReferenceSeed.Obter(
            client, "/api/cidades/proximas?lat=-5.35&long=-49.13&raioKm=200&limit=1");
        using JsonDocument doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());

        doc.RootElement.GetArrayLength().Should().Be(1);
        doc.RootElement[0].GetProperty("nome").GetString().Should().Be("Marabá");
    }

    [Fact(DisplayName = "CA-07: Accept vendor incompatível retorna 406; cidade-proxima.v1 é aceito")]
    public async Task CidadesProximas_VendorMime_406()
    {
        await SemearAsync();
        using HttpClient client = _fixture.Factory.CreateClient();
        const string rota = "/api/cidades/proximas?lat=-5.35&long=-49.13&raioKm=100";

        using HttpRequestMessage incompativel = new(HttpMethod.Get, rota);
        incompativel.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.uniplus.cidade-proxima.v2+json"));
        using HttpResponseMessage resp406 = await client.SendAsync(incompativel);
        resp406.StatusCode.Should().Be(HttpStatusCode.NotAcceptable);

        using HttpRequestMessage compativel = new(HttpMethod.Get, rota);
        compativel.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.uniplus.cidade-proxima.v1+json"));
        using HttpResponseMessage resp200 = await client.SendAsync(compativel);
        resp200.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "CA-08: ponto é Point(long,lat); inverter lat/long muda o resultado; precisão decimal(9,6)")]
    public async Task CidadesProximas_OrdemCoordenadas_LongLat()
    {
        await SemearAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        // Correto: (lat=-5.35, long=-49.13) próximo a Marabá.
        using HttpResponseMessage correto = await GeoReferenceSeed.Obter(
            client, "/api/cidades/proximas?lat=-5.35&long=-49.13&raioKm=50");
        using JsonDocument docOk = JsonDocument.Parse(await correto.Content.ReadAsStringAsync());
        JsonElement maraba = docOk.RootElement.EnumerateArray()
            .First(c => c.GetProperty("nome").GetString() == "Marabá");

        // Precisão: Latitude/Longitude espelham o seed em decimal(9,6).
        maraba.GetProperty("latitude").GetDecimal().Should().Be(-5.36867m);
        maraba.GetProperty("longitude").GetDecimal().Should().Be(-49.11731m);

        // Invertido (lat=-49.13, long=-5.35): ambos válidos, mas caem no Atlântico Sul,
        // longe de Marabá → a cidade não aparece. Prova a convenção X=long, Y=lat.
        using HttpResponseMessage invertido = await GeoReferenceSeed.Obter(
            client, "/api/cidades/proximas?lat=-49.13&long=-5.35&raioKm=50");
        using JsonDocument docInv = JsonDocument.Parse(await invertido.Content.ReadAsStringAsync());
        List<string> nomesInv = [.. docInv.RootElement.EnumerateArray().Select(c => c.GetProperty("nome").GetString()!)];
        nomesInv.Should().NotContain("Marabá");
    }

    private static Point Ponto(decimal latitude, decimal longitude) =>
        new((double)longitude, (double)latitude) { SRID = 4326 };

    private async Task SemearAsync()
    {
        await GeoReferenceSeed.LimparAsync(_fixture);
        await using GeoDbContext ctx = _fixture.CreateDbContext();

        Pais brasil = GeoReferenceSeed.NovoBrasil();
        Estado para = GeoReferenceSeed.NovoEstado(brasil.Id, "PA", "Pará");
        Estado saoPauloUf = GeoReferenceSeed.NovoEstado(brasil.Id, "SP", "São Paulo");

        // Próximas ao ponto de consulta (-5.35, -49.13):
        Cidade maraba = GeoReferenceSeed.NovaCidade(
            para.Id, "PA", "1500402", "Marabá",
            latitude: -5.36867m, longitude: -49.11731m, coordenada: Ponto(-5.36867m, -49.11731m));
        Cidade eldorado = GeoReferenceSeed.NovaCidade(
            para.Id, "PA", "1502608", "Eldorado dos Carajás",
            latitude: -6.10m, longitude: -49.36m, coordenada: Ponto(-6.10m, -49.36m));
        // Longe (> 100 km):
        Cidade saoPaulo = GeoReferenceSeed.NovaCidade(
            saoPauloUf.Id, "SP", "3550308", "São Paulo",
            latitude: -23.55m, longitude: -46.63m, coordenada: Ponto(-23.55m, -46.63m));
        // Sem coordenada (CA-05): próxima ao ponto mas Coordenada null → não aparece.
        Cidade semCoordenada = GeoReferenceSeed.NovaCidade(
            para.Id, "PA", "1500800", "Bom Jesus do Tocantins");
        // Coordenada presente mas lat/long nulos (P2 do review): também excluída.
        Cidade coordSemLatLong = GeoReferenceSeed.NovaCidade(
            para.Id, "PA", "1500909", "Brejo Grande do Araguaia",
            coordenada: Ponto(-5.36m, -49.12m));

        // Logradouros próximos (cidade Marabá):
        Logradouro ruaA = GeoReferenceSeed.NovoLogradouro(
            maraba.Id, "PA", "68500000", "A", tipo: "Rua", nomeCompleto: "Rua A",
            latitude: -5.355m, longitude: -49.135m, coordenada: Ponto(-5.355m, -49.135m));
        Logradouro avenidaB = GeoReferenceSeed.NovoLogradouro(
            maraba.Id, "PA", "68500100", "B", tipo: "Avenida", nomeCompleto: "Avenida B",
            latitude: -5.40m, longitude: -49.20m, coordenada: Ponto(-5.40m, -49.20m));
        // Logradouro longe (São Paulo):
        Logradouro pracaSe = GeoReferenceSeed.NovoLogradouro(
            saoPaulo.Id, "SP", "01001000", "da Sé", tipo: "Praça", nomeCompleto: "Praça da Sé",
            latitude: -23.55m, longitude: -46.63m, coordenada: Ponto(-23.55m, -46.63m));

        ctx.Paises.Add(brasil);
        ctx.Estados.AddRange(para, saoPauloUf);
        ctx.Cidades.AddRange(maraba, eldorado, saoPaulo, semCoordenada, coordSemLatLong);
        ctx.Logradouros.AddRange(ruaA, avenidaB, pracaSe);
        await ctx.SaveChangesAsync();
    }
}
