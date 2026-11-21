namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.FatosCandidato;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

/// <summary>
/// Smoke dos endpoints somente-leitura do catálogo <c>fatos_do_candidato</c>
/// (UNI-REQ-0077, ADR-0111) com Wolverine contra Postgres efêmero: routing, vendor
/// media type, resolução do leitor cross-módulo via DI ponta-a-ponta, e 404 por
/// chave natural inexistente.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class FatoCandidatoEndpointTests
{
    private readonly ConfiguracaoEndpointFixture _fixture;

    public FatoCandidatoEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "GET /api/configuracao/fatos-candidato retorna 200 com vendor MIME e os fatos de sistema")]
    public async Task Listar_Retorna200ComOsFatosDeSistema()
    {
        using HttpResponseMessage response = await GetAsync("/api/configuracao/fatos-candidato");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/vnd.uniplus.fato-candidato.v1+json");
        using JsonDocument corpo = await LerAsync(response);
        List<FatoCandidatoView> fatos = [.. corpo.RootElement.EnumerateArray().Select(Fato)];
        // O catálogo é compartilhado pela coleção, e outros testes cadastram fatos do administrador:
        // confere os fatos de sistema, não a contagem total.
        fatos.Select(f => f.Codigo).Should().Contain(FatoCandidatoSeed.Itens.Select(i => i.Codigo));
        fatos.Select(f => f.Codigo).Should().BeInAscendingOrder(StringComparer.Ordinal);
        fatos.Should().Contain(f => f.Codigo == "MODALIDADE" && f.Cardinalidade == "MULTIVALORADO" && f.ValoresDominio == null);
    }

    [Fact(DisplayName = "GET /api/configuracao/fatos-candidato/{codigo} resolve pela chave natural")]
    public async Task ObterPorCodigo_Resolve()
    {
        using HttpResponseMessage response = await GetAsync("/api/configuracao/fatos-candidato/COR_RACA");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument corpo = await LerAsync(response);
        FatoCandidatoView fato = Fato(corpo.RootElement);
        fato.Codigo.Should().Be("COR_RACA");
        fato.Dominio.Should().Be("CATEGORICO");
        // A view projeta os códigos dos valores declarados em ValoresDominio, que o consumidor
        // cross-módulo (PredicadoDnfValidador) usa como domínio do categórico estático.
        fato.ValoresDominio.Should().Contain("PRETA");
        fato.ValoresDominioDeclarados.Should().NotBeNull().And.Contain(v => v.Codigo == "PRETA");
    }

    [Fact(DisplayName = "GET /api/configuracao/fatos-candidato/{codigo} devolve os operadores do fato, na ordem da tela e com o rótulo da cardinalidade")]
    public async Task ObterPorCodigo_DevolveOsOperadoresAdmitidos()
    {
        using HttpResponseMessage response = await GetAsync("/api/configuracao/fatos-candidato/COR_RACA");

        using JsonDocument corpo = await LerAsync(response);
        JsonElement[] operadores = [.. corpo.RootElement.GetProperty("operadoresAdmitidos").EnumerateArray()];
        operadores.Select(static o => o.GetProperty("codigo").GetString()).Should().Equal("IGUAL", "DIFERENTE", "EM", "NAO_EM");
        operadores.Select(static o => o.GetProperty("rotulo").GetString())
            .Should().Equal("é igual a", "é diferente de", "é um de", "não é nenhum de");
    }

    [Fact(DisplayName = "GET /api/configuracao/fatos-candidato devolve lista vazia de operadores para o fato que nenhuma regra cita")]
    public async Task Listar_FatoDeTexto_DevolveOperadoresVazios()
    {
        using HttpResponseMessage response = await GetAsync("/api/configuracao/fatos-candidato");

        using JsonDocument corpo = await LerAsync(response);
        corpo.RootElement.EnumerateArray().Should().Contain(static item =>
            item.GetProperty("fato").GetProperty("dominio").GetString() == "TEXTO"
            && item.GetProperty("operadoresAdmitidos").GetArrayLength() == 0);
    }

    [Fact(DisplayName = "GET /api/configuracao/fatos-candidato/{codigo} retorna 404 para código inexistente")]
    public async Task ObterPorCodigo_Inexistente_Retorna404()
    {
        using HttpResponseMessage response = await GetAsync("/api/configuracao/fatos-candidato/NAO_EXISTE");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static FatoCandidatoView Fato(JsonElement item) =>
        item.GetProperty("fato").Deserialize<FatoCandidatoView>(JsonSerializerOptions.Web)!;

    private static async Task<JsonDocument> LerAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    private async Task<HttpResponseMessage> GetAsync(string caminho)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        return await client.GetAsync(new Uri(caminho, UriKind.Relative));
    }
}
