namespace Unifesspa.UniPlus.Configuracao.IntegrationTests;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Infrastructure;

using Unifesspa.UniPlus.IntegrationTests.Fixtures;

/// <summary>
/// Smoke + drift check do endpoint <c>/openapi/configuracao.json</c>: runtime
/// gera o spec OpenAPI 3.x do módulo Configuração e compara com o baseline
/// committed em <c>contracts/openapi.configuracao.json</c> (ADR-0030).
/// </summary>
public sealed class OpenApiEndpointTests : IClassFixture<ConfiguracaoApiFactory>
{
    private const string BaselineRelativePath = "contracts/openapi.configuracao.json";

    private readonly ConfiguracaoApiFactory _factory;

    public OpenApiEndpointTests(ConfiguracaoApiFactory factory)
    {
        _factory = factory;
    }

    [Fact(DisplayName = "GET /openapi/configuracao.json retorna 200 com documento OpenAPI válido e metadata Uni+")]
    public async Task GetOpenApiDocument_RetornaSpecJsonComMetadataInjetada()
    {
        using HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(new Uri("/openapi/configuracao.json", UriKind.Relative));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        root.GetProperty("openapi").GetString().Should().StartWith("3.");
        root.GetProperty("info").GetProperty("version").GetString().Should().Be("1.0.0");
    }

    [Fact(DisplayName = "O código do programa de oferta é componente de texto com enum, sem o valor nenhum")]
    public Task ProgramaDeOfertaCodigo_EComponenteDeTextoComEnum() =>
        ConferirComponenteDeCodigosAsync(
            "ProgramaDeOfertaCodigo",
            ["REGULAR", "FORMA_PARA", "PARFOR", "PRONERA", "PEPETI", "CONVENIO_OUTRO", "OUTRO"]);

    [Fact(DisplayName = "O código do formato pedagógico é componente de texto com enum, sem o valor nenhum")]
    public Task FormatoPedagogicoCodigo_EComponenteDeTextoComEnum() =>
        ConferirComponenteDeCodigosAsync("FormatoPedagogicoCodigo", ["PRESENCIAL", "SEMIPRESENCIAL", "EAD"]);

    [Fact(DisplayName = "O código da resposta do programa de oferta referencia o componente do enum")]
    public Task RespostaDoPrograma_ReferenciaOComponenteDoEnum() =>
        ConferirReferenciaDaRespostaAsync("ProgramaDeOferta", "programaDeOferta", "ProgramaDeOfertaCodigo");

    [Fact(DisplayName = "O código da resposta do formato pedagógico referencia o componente do enum")]
    public Task RespostaDoFormato_ReferenciaOComponenteDoEnum() =>
        ConferirReferenciaDaRespostaAsync("FormatoPedagogico", "formatoPedagogico", "FormatoPedagogicoCodigo");

    [Theory(DisplayName = "Os pedidos de criação e de atualização da oferta documentam programa e formato com o componente do enum")]
    [InlineData("CriarOfertaCursoCommand")]
    [InlineData("AtualizarOfertaCursoCommand")]
    public async Task PedidosDaOferta_ReferenciamOsComponentesDosEnums(string pedido)
    {
        JsonElement schemas = await ObterSchemasAsync();
        JsonElement propriedades = schemas.GetProperty(pedido).GetProperty("properties");

        ReferenciasDe(propriedades.GetProperty("programaDeOferta")).Should().Contain("#/components/schemas/ProgramaDeOfertaCodigo");
        ReferenciasDe(propriedades.GetProperty("formatoPedagogico")).Should().Contain("#/components/schemas/FormatoPedagogicoCodigo");
    }

    private async Task ConferirComponenteDeCodigosAsync(string componente, string[] codigosEsperados)
    {
        JsonElement codigos = (await ObterSchemasAsync()).GetProperty(componente);

        codigos.GetProperty("type").GetString().Should().Be("string");
        codigos.GetProperty("enum").EnumerateArray().Select(c => c.GetString()).Should().Equal(codigosEsperados);
    }

    private async Task ConferirReferenciaDaRespostaAsync(string resposta, string campo, string componente)
    {
        JsonElement schemas = await ObterSchemasAsync();

        schemas.GetProperty(resposta).GetProperty("properties").GetProperty("codigo").GetProperty("$ref").GetString()
            .Should().Be($"#/components/schemas/{componente}");
        schemas.GetProperty("OfertaCursoDto").GetProperty("properties").GetProperty(campo).GetProperty("$ref").GetString()
            .Should().Be($"#/components/schemas/{resposta}");
    }

    private static IEnumerable<string?> ReferenciasDe(JsonElement propriedade) =>
        propriedade.GetProperty("oneOf").EnumerateArray()
            .Where(alternativa => alternativa.TryGetProperty("$ref", out _))
            .Select(alternativa => alternativa.GetProperty("$ref").GetString());

    private async Task<JsonElement> ObterSchemasAsync()
    {
        using JsonDocument doc = JsonDocument.Parse(await ObterDocumentoAsync());

        return doc.RootElement.GetProperty("components").GetProperty("schemas").Clone();
    }

    [Fact(DisplayName = "Spec runtime de Configuração bate com baseline committed em contracts/")]
    public async Task SpecRuntime_DeveCasarComBaselineCommitted()
    {
        using HttpClient client = _factory.CreateClient();

        await ContratoOpenApiGravado.ConferirAsync(client, "/openapi/configuracao.json", BaselineRelativePath);
    }

    [Fact(DisplayName = "No contrato da Configuração, todo campo de texto que leva o código de um enum referencia o componente dos códigos")]
    public async Task CampoDeTextoComCodigoDeEnum_ReferenciaOComponenteDosCodigos()
    {
        using HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync(new Uri("/openapi/configuracao.json", UriKind.Relative));
        response.EnsureSuccessStatusCode();
        using JsonDocument spec = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        CodigosDeEnumNoContrato.CamposComTextoCru(spec.RootElement).Should().BeEmpty(
            "o campo que leva código de enum de contrato publica o componente dos códigos do enum (ADR-0176)");
        CodigosDeEnumNoContrato.ReferenciaOsCodigos(spec.RootElement, "CondicaoDoPredicadoDto", "operador").Should().BeTrue();
        CodigosDeEnumNoContrato.ReferenciaOsCodigos(spec.RootElement, "ApresentacaoDoCampoDto", "tipoRenderizacao").Should().BeTrue();
    }

    private async Task<string> ObterDocumentoAsync()
    {
        using HttpClient client = _factory.CreateClient();
        using HttpResponseMessage response = await client.GetAsync(new Uri("/openapi/configuracao.json", UriKind.Relative));
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync();
    }
}
