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
/// Lookup de CEP (#676) contra a API real + PostGIS. Read-only
/// <c>[AllowAnonymous]</c>. Cobre CA-01..CA-06 e CA-09 (a cobertura de cache-aside
/// CA-07/CA-08 fica nos testes de unidade do <c>CepResolver</c>, que provam "hit não
/// toca o banco" e "404 não é cacheado" com precisão). O cache degrada para o banco
/// (Redis vazio na <see cref="GeoApiFactory"/>). A collection é serial; cada teste
/// TRUNCA e semeia.
/// </summary>
[Collection(GeoPostgisCollection.Name)]
public sealed class CepEndpointTests
{
    private readonly GeoPostgisFixture _fixture;

    public CepEndpointTests(GeoPostgisFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "CA-01: CEP de logradouro resolve logradouro+bairro+cidade+UF+coordenada e _links")]
    public async Task Cep_ResolveLogradouro()
    {
        await SemearAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await GeoReferenceSeed.Obter(client, "/api/cep/01001000");
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        JsonElement raiz = doc.RootElement;
        raiz.GetProperty("cep").GetString().Should().Be("01001000");
        raiz.GetProperty("tipo").GetString().Should().Be("Praça");
        raiz.GetProperty("logradouro").GetString().Should().Be("Praça da Sé");
        raiz.GetProperty("bairro").GetString().Should().Be("Sé");
        raiz.GetProperty("cidade").GetString().Should().Be("São Paulo");
        raiz.GetProperty("codigoIbge").GetString().Should().Be("3550308");
        raiz.GetProperty("uf").GetString().Should().Be("SP");
        raiz.GetProperty("latitude").GetDecimal().Should().Be(-23.55m);
        raiz.GetProperty("longitude").GetDecimal().Should().Be(-46.63m);
        raiz.GetProperty("nivelResolucao").GetString().Should().Be("logradouro");
        raiz.GetProperty("origem").GetString().Should().Be("logradouro");
        raiz.GetProperty("alternativos").GetArrayLength().Should().Be(0);

        JsonElement links = raiz.GetProperty("_links");
        links.GetProperty("cidade").GetString().Should().Be("/api/cidades/3550308");
        links.GetProperty("estado").GetString().Should().Be("/api/estados/SP");
    }

    [Fact(DisplayName = "CA-01: complemento único do CEP é exposto")]
    public async Task Cep_ComplementoUnico()
    {
        await SemearAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await GeoReferenceSeed.Obter(client, "/api/cep/01001000");
        using JsonDocument doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("complemento").GetString().Should().Be("de 1 a 999 - lado ímpar");
    }

    [Fact(DisplayName = "CEP com mais de um complemento: campo complemento fica null (ambíguo)")]
    public async Task Cep_MultiplosComplementos_Null()
    {
        await SemearAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await GeoReferenceSeed.Obter(client, "/api/cep/01002000");
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        JsonElement complemento = doc.RootElement.GetProperty("complemento");
        complemento.ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact(DisplayName = "CA-02: CEP com vários logradouros traz primário + alternativos, estável entre execuções")]
    public async Task Cep_MultiplosLogradouros_DesempateEstavel()
    {
        await SemearAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        async Task<(string Primario, IReadOnlyList<string> Alternativos)> ResolverAsync()
        {
            using HttpResponseMessage resposta = await GeoReferenceSeed.Obter(client, "/api/cep/01310100");
            resposta.StatusCode.Should().Be(HttpStatusCode.OK);
            using JsonDocument doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
            JsonElement raiz = doc.RootElement;
            raiz.GetProperty("nivelResolucao").GetString().Should().Be("logradouro");
            string primario = raiz.GetProperty("logradouro").GetString()!;
            List<string> alternativos = [.. raiz.GetProperty("alternativos")
                .EnumerateArray()
                .Select(a => a.GetProperty("logradouro").GetString()!)];
            return (primario, alternativos);
        }

        (string Primario, IReadOnlyList<string> Alternativos) primeira = await ResolverAsync();
        (string Primario, IReadOnlyList<string> Alternativos) segunda = await ResolverAsync();

        // Desempate (nome_normalizado, ...): "alameda santos" < "avenida paulista".
        primeira.Primario.Should().Be("Alameda Santos");
        primeira.Alternativos.Should().ContainSingle().Which.Should().Be("Avenida Paulista");
        segunda.Primario.Should().Be(primeira.Primario, "o primário é determinístico entre execuções");
        segunda.Alternativos.Should().Equal(primeira.Alternativos, "a ordem dos alternativos é estável");
    }

    [Fact(DisplayName = "CA-03: CEP geral sem logradouro resolve cidade por faixa (nível cidade, sem logradouro)")]
    public async Task Cep_ResolvePorFaixaCidade()
    {
        await SemearAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await GeoReferenceSeed.Obter(client, "/api/cep/68500005");
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        JsonElement raiz = doc.RootElement;
        raiz.GetProperty("cidade").GetString().Should().Be("Marabá");
        raiz.GetProperty("uf").GetString().Should().Be("PA");
        raiz.GetProperty("origem").GetString().Should().Be("faixa-cidade");
        raiz.GetProperty("nivelResolucao").GetString().Should().Be("cidade");
        raiz.GetProperty("logradouro").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact(DisplayName = "CA-03: faixa de bairro mais específica eleva o nível para bairro")]
    public async Task Cep_ResolvePorFaixaBairro()
    {
        await SemearAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await GeoReferenceSeed.Obter(client, "/api/cep/68507100");
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        JsonElement raiz = doc.RootElement;
        raiz.GetProperty("cidade").GetString().Should().Be("Marabá");
        raiz.GetProperty("bairro").GetString().Should().Be("Cidade Nova");
        raiz.GetProperty("origem").GetString().Should().Be("faixa-bairro");
        raiz.GetProperty("nivelResolucao").GetString().Should().Be("bairro");
    }

    [Fact(DisplayName = "CA-04: CEP de grande usuário traz nome + cidade/UF da faixa CEP")]
    public async Task Cep_ResolveGrandeUsuario_CidadeDaFaixa()
    {
        await SemearAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await GeoReferenceSeed.Obter(client, "/api/cep/01051900");
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        JsonElement raiz = doc.RootElement;
        raiz.GetProperty("origem").GetString().Should().Be("grande-usuario");
        raiz.GetProperty("logradouro").GetString().Should().Be("Banco do Brasil Agência Centro");
        raiz.GetProperty("cidade").GetString().Should().Be("São Paulo");
        raiz.GetProperty("uf").GetString().Should().Be("SP");
        raiz.GetProperty("nivelResolucao").GetString().Should().Be("cidade");
    }

    [Fact(DisplayName = "CA-05: CEP com máscara é normalizado e resolvido")]
    public async Task Cep_ComMascara_Resolve()
    {
        await SemearAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await GeoReferenceSeed.Obter(client, "/api/cep/01001-000");
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("logradouro").GetString().Should().Be("Praça da Sé");
    }

    [Theory(DisplayName = "CA-05: CEP malformado (≠8 dígitos, não-numérico) retorna 400")]
    [InlineData("/api/cep/123")]
    [InlineData("/api/cep/010010000")]
    [InlineData("/api/cep/0100100a")]
    [InlineData("/api/cep/abcdefgh")]
    public async Task Cep_Malformado_400(string rota)
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await GeoReferenceSeed.Obter(client, rota);
        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "CA-06: CEP bem-formado sem cobertura retorna 404")]
    public async Task Cep_Inexistente_404()
    {
        await SemearAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await GeoReferenceSeed.Obter(client, "/api/cep/99999999");
        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "ADR-0092: logradouro stale (vigente=false) não resolve (404 quando nada mais cobre)")]
    public async Task Cep_LogradouroStale_NaoResolve()
    {
        await SemearAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        // 30000000 só tem um logradouro stale e não está em nenhuma faixa → 404.
        using HttpResponseMessage resposta = await GeoReferenceSeed.Obter(client, "/api/cep/30000000");
        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "CA-09: Accept vendor incompatível retorna 406; vendor cep.v1 é aceito")]
    public async Task Cep_VendorMime_406()
    {
        await SemearAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpRequestMessage incompativel = new(HttpMethod.Get, "/api/cep/01001000");
        incompativel.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.uniplus.cep.v2+json"));
        using HttpResponseMessage resp406 = await client.SendAsync(incompativel);
        resp406.StatusCode.Should().Be(HttpStatusCode.NotAcceptable);

        using HttpRequestMessage compativel = new(HttpMethod.Get, "/api/cep/01001000");
        compativel.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.uniplus.cep.v1+json"));
        using HttpResponseMessage resp200 = await client.SendAsync(compativel);
        resp200.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task SemearAsync()
    {
        await GeoReferenceSeed.LimparAsync(_fixture);
        await using GeoDbContext ctx = _fixture.CreateDbContext();

        Pais brasil = GeoReferenceSeed.NovoBrasil();
        Estado saoPaulo = GeoReferenceSeed.NovoEstado(brasil.Id, "SP", "São Paulo", regiao: "Sudeste", capital: "São Paulo");
        Estado para = GeoReferenceSeed.NovoEstado(brasil.Id, "PA", "Pará", regiao: "Norte", capital: "Belém");

        Cidade cidadeSp = GeoReferenceSeed.NovaCidade(saoPaulo.Id, "SP", "3550308", "São Paulo");
        Cidade maraba = GeoReferenceSeed.NovaCidade(para.Id, "PA", "1500402", "Marabá");

        Bairro se = GeoReferenceSeed.NovoBairro(cidadeSp.Id, "SP", "Sé");
        Bairro belaVista = GeoReferenceSeed.NovoBairro(cidadeSp.Id, "SP", "Bela Vista");
        Bairro jardimPaulista = GeoReferenceSeed.NovoBairro(cidadeSp.Id, "SP", "Jardim Paulista");
        Bairro cidadeNova = GeoReferenceSeed.NovoBairro(maraba.Id, "PA", "Cidade Nova");

        // (1) CEP de logradouro único (CA-01) + 1 complemento.
        Logradouro praca = GeoReferenceSeed.NovoLogradouro(
            cidadeSp.Id, "SP", "01001000", "Praça da Sé", tipo: "Praça",
            bairroId: se.Id, latitude: -23.55m, longitude: -46.63m);
        LogradouroComplemento complementoPraca = GeoReferenceSeed.NovoComplemento("01001000", "de 1 a 999 - lado ímpar");

        // CEP com 2 complementos → campo complemento null.
        Logradouro rua = GeoReferenceSeed.NovoLogradouro(
            cidadeSp.Id, "SP", "01002000", "Rua das Flores", tipo: "Rua", bairroId: se.Id);
        LogradouroComplemento comp1 = GeoReferenceSeed.NovoComplemento("01002000", "lado par");
        LogradouroComplemento comp2 = GeoReferenceSeed.NovoComplemento("01002000", "lado ímpar");

        // (2) CEP compartilhado por 2 logradouros (CA-02). Desempate por nome_normalizado.
        Logradouro avPaulista = GeoReferenceSeed.NovoLogradouro(
            cidadeSp.Id, "SP", "01310100", "Avenida Paulista", tipo: "Avenida", bairroId: belaVista.Id);
        Logradouro alSantos = GeoReferenceSeed.NovoLogradouro(
            cidadeSp.Id, "SP", "01310100", "Alameda Santos", tipo: "Alameda", bairroId: jardimPaulista.Id);

        // Logradouro stale (não resolve) num CEP sem faixa.
        Logradouro stale = GeoReferenceSeed.NovoLogradouro(
            cidadeSp.Id, "SP", "30000000", "Rua Obsoleta", vigente: false);

        // (3) Faixas de CEP.
        CidadeFaixaCep faixaSp = GeoReferenceSeed.NovaCidadeFaixa(cidadeSp.Id, "01000000", "01099999");
        CidadeFaixaCep faixaMaraba = GeoReferenceSeed.NovaCidadeFaixa(maraba.Id, "68500000", "68599999");
        BairroFaixaCep faixaCidadeNova = GeoReferenceSeed.NovaBairroFaixa(cidadeNova.Id, "68507000", "68507999");

        // (4) Grande usuário (sem logradouro próprio; cidade vem da faixa SP).
        CepGrandeUsuario grandeUsuario = GeoReferenceSeed.NovoGrandeUsuario("01051900", "Banco do Brasil Agência Centro");

        ctx.Paises.Add(brasil);
        ctx.Estados.AddRange(saoPaulo, para);
        ctx.Cidades.AddRange(cidadeSp, maraba);
        ctx.Bairros.AddRange(se, belaVista, jardimPaulista, cidadeNova);
        ctx.Logradouros.AddRange(praca, rua, avPaulista, alSantos, stale);
        ctx.LogradouroComplementos.AddRange(complementoPraca, comp1, comp2);
        ctx.CidadeFaixasCep.AddRange(faixaSp, faixaMaraba);
        ctx.BairroFaixasCep.Add(faixaCidadeNova);
        ctx.CepGrandesUsuarios.Add(grandeUsuario);
        await ctx.SaveChangesAsync();
    }
}
