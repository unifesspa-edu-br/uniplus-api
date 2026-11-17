namespace Unifesspa.UniPlus.Publicacoes.IntegrationTests.TiposAtoPublicado;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Publicacoes.IntegrationTests.Infrastructure;

using static TipoAtoPublicadoApoio;

/// <summary>
/// Leitura pública dos endpoints de <c>TipoAtoPublicado</c> contra o monólito real com
/// Postgres efêmero: routing, vendor media type, HATEOAS, versão vigente e negociação de conteúdo.
/// </summary>
[Collection(PublicacoesEndpointCollection.Name)]
public sealed class TipoAtoPublicadoLeituraEndpointTests
{
    private readonly PublicacoesEndpointFixture _fixture;
    private readonly TipoAtoPublicadoApoio _apoio;

    public TipoAtoPublicadoLeituraEndpointTests(PublicacoesEndpointFixture fixture)
    {
        _fixture = fixture;
        _apoio = new TipoAtoPublicadoApoio(fixture);
    }

    // ── Leitura pública ──────────────────────────────────────────────────────

    [Fact(DisplayName = "GET tipos-ato retorna 200 com Content-Type vendor MIME")]
    public async Task Listar_Retorna200ComVendorMime()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(new Uri(Base, UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType
            .Should().Be("application/vnd.uniplus.tipo-ato.v1+json");
    }

    [Fact(DisplayName = "GET tipos-ato/{id} retorna 404 quando inexistente")]
    public async Task ObterPorId_NaoExiste_Retorna404()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri($"{Base}/{Guid.NewGuid()}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "GET tipos-ato/{id} devolve _links.self e _links.collection resolvidos")]
    public async Task ObterPorId_TrazLinksResolvidos()
    {
        string codigo = CodigoUnico();
        Guid id = await _apoio.CriarAsync(codigo);

        using HttpClient client = _fixture.Factory.CreateClient();
        using JsonDocument doc = await LerAsync(client, new Uri($"{Base}/{id}", UriKind.Relative));
        JsonElement links = doc.RootElement.GetProperty("_links");

        // Asserir o valor, não a presença: um LinkGenerator apontando para a action
        // errada devolveria um path qualquer e passaria por qualquer teste de status.
        links.GetProperty("self").GetString().Should().Be($"{Base}/{id}");
        links.GetProperty("collection").GetString().Should().Be(Base);
    }

    [Fact(DisplayName = "GET tipos-ato traz _links em cada item da coleção")]
    public async Task Listar_ItensTrazemLinks()
    {
        Guid id = await _apoio.CriarAsync(CodigoUnico());

        using HttpClient client = _fixture.Factory.CreateClient();
        using JsonDocument doc = await LerAsync(client, new Uri($"{Base}?limit=100", UriKind.Relative));
        JsonElement item = doc.RootElement.EnumerateArray()
            .Single(e => e.GetProperty("id").GetGuid() == id);

        item.GetProperty("_links").GetProperty("self").GetString().Should().Be($"{Base}/{id}");
    }

    [Fact(DisplayName = "A listagem pública não expõe versões de vigência futura")]
    public async Task Listar_NaoExpoeVersaoFutura()
    {
        string codigo = CodigoUnico();
        Guid futura = await _apoio.CriarAsync(codigo, inicio: "2099-01-01");

        using HttpClient client = _fixture.Factory.CreateClient();

        // Uma versão que ainda não vale é planejamento normativo não anunciado.
        (await IdsAsync(client, $"{Base}?limit=100")).Should().NotContain(futura);

        // A série histórica continua acessível a quem a pedir explicitamente.
        (await IdsAsync(client, $"{Base}?limit=100&vigentes=false")).Should().Contain(futura);
    }

    [Fact(DisplayName = "A listagem pública não expõe versões já encerradas")]
    public async Task Listar_NaoExpoeVersaoEncerrada()
    {
        string codigo = CodigoUnico();
        Guid encerrada = await _apoio.CriarAsync(codigo, inicio: "2020-01-01", fim: "2021-01-01");

        using HttpClient client = _fixture.Factory.CreateClient();

        (await IdsAsync(client, $"{Base}?limit=100")).Should().NotContain(encerrada);
        (await IdsAsync(client, $"{Base}?limit=100&vigentes=false")).Should().Contain(encerrada);
    }

    [Fact(DisplayName = "A autoria não é exposta no contrato público")]
    public async Task Dto_NaoExpoeAutoria()
    {
        Guid id = await _apoio.CriarAsync(CodigoUnico());

        using HttpClient client = _fixture.Factory.CreateClient();
        using JsonDocument doc = await LerAsync(client, new Uri($"{Base}/{id}", UriKind.Relative));
        doc.RootElement.TryGetProperty("createdBy", out _).Should().BeFalse();
        doc.RootElement.TryGetProperty("updatedBy", out _).Should().BeFalse();
    }

    // ── Vigente ──────────────────────────────────────────────────────────────

    [Fact(DisplayName = "GET tipos-ato/{codigo}/vigente numa data histórica devolve a versão de então")]
    public async Task ObterVigente_DataHistorica_DevolveVersaoDaEpoca()
    {
        string codigo = CodigoUnico();
        Guid antiga = await _apoio.CriarAsync(codigo, inicio: "2026-01-01", fim: "2026-06-01", congela: false);
        Guid nova = await _apoio.CriarAsync(codigo, inicio: "2026-06-01", fim: null, congela: true);

        using HttpClient client = _fixture.Factory.CreateClient();

        (await VigenteIdAsync(client, codigo, "2026-03-15")).Should().Be(antiga);
        (await VigenteIdAsync(client, codigo, "2026-05-31")).Should().Be(antiga);

        // O fim é exclusivo: no dia da fronteira já vale a sucessora.
        (await VigenteIdAsync(client, codigo, "2026-06-01")).Should().Be(nova);
    }

    [Fact(DisplayName = "GET vigente sem data usa hoje")]
    public async Task ObterVigente_SemData_UsaHoje()
    {
        string codigo = CodigoUnico();
        Guid id = await _apoio.CriarAsync(codigo, inicio: "2020-01-01");

        using HttpClient client = _fixture.Factory.CreateClient();
        using JsonDocument doc = await LerAsync(client, new Uri($"{Base}/{codigo}/vigente", UriKind.Relative));
        doc.RootElement.GetProperty("id").GetGuid().Should().Be(id);
    }

    [Fact(DisplayName = "GET vigente de código inexistente retorna 404")]
    public async Task ObterVigente_CodigoInexistente_Retorna404()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri($"{Base}/{CodigoUnico()}/vigente", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <remarks>
    /// 404 diria "este tipo não existe"; a verdade é "isto não é um código". O corpo tem de continuar
    /// dizendo qual é o formato: esta recusa vem de um validador que roda depois de o binding ter dado
    /// certo e divide o status 400 com as recusas de leitura da requisição, genéricas por natureza.
    /// Cobrar só o status deixaria passar a troca da orientação específica por um "não foi possível
    /// ler o corpo", que além de inútil aqui seria falso.
    /// </remarks>
    [Theory(DisplayName = "GET vigente com código malformado retorna 400, não 404")]
    [InlineData("edital_abertura")]
    [InlineData("Edital")]
    [InlineData("EDITAL-ABERTURA")]
    [InlineData("EDITAL__ABERTURA")]
    public async Task ObterVigente_CodigoMalformado_Retorna400(string codigo)
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri($"{Base}/{codigo}/vigente", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        string corpo = await response.Content.ReadAsStringAsync();
        corpo.Should().Contain("letras maiúsculas",
            "a mensagem do validador é o que diz ao cliente como corrigir o código");
    }

    [Fact(DisplayName = "GET vigente com um GUID no lugar do código retorna 400")]
    public async Task ObterVigente_ComGuid_Retorna400()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri($"{Base}/{Guid.NewGuid()}/vigente", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Negociação de conteúdo ───────────────────────────────────────────────

    [Fact(DisplayName = "Accept com vendor MIME de versão inexistente retorna 406")]
    public async Task Listar_ComVersaoInexistente_Retorna406()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, new Uri(Base, UriKind.Relative));
        request.Headers.Add("Accept", "application/vnd.uniplus.tipo-ato.v99+json");

        (await client.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.NotAcceptable);
    }

    private static async Task<Guid> VigenteIdAsync(HttpClient client, string codigo, string data)
    {
        using JsonDocument doc = await LerAsync(
            client, new Uri($"{Base}/{codigo}/vigente?data={data}", UriKind.Relative));
        return doc.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<IReadOnlyList<Guid>> IdsAsync(HttpClient client, string url)
    {
        using JsonDocument doc = await LerAsync(client, new Uri(url, UriKind.Relative));
        return [.. doc.RootElement.EnumerateArray().Select(e => e.GetProperty("id").GetGuid())];
    }
}
