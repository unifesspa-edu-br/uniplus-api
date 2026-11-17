namespace Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.IntegrationTests.Fixtures;
using Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Infrastructure;

/// <summary>
/// Confere o endpoint <c>/openapi/organizacao.json</c>: a API em execução gera a especificação OpenAPI 3.x
/// do módulo Organização Institucional, e o teste a compara com o contrato gravado em
/// <c>contracts/openapi.organizacao.json</c> (ADR-0030).
/// </summary>
public sealed class OpenApiEndpointTests : IClassFixture<OrganizacaoApiFactory>
{
    private const string CaminhoDoContratoGravado = "contracts/openapi.organizacao.json";
    private const string RotaDaEspecificacao = "/openapi/organizacao.json";

    private readonly OrganizacaoApiFactory _factory;

    public OpenApiEndpointTests(OrganizacaoApiFactory factory)
    {
        _factory = factory;
    }

    [Fact(DisplayName = "GET /openapi/organizacao.json retorna 200 com documento OpenAPI válido e metadata Uni+")]
    public async Task GetOpenApiDocument_RetornaSpecJsonComMetadataInjetada()
    {
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(new Uri(RotaDaEspecificacao, UriKind.Relative));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement info = doc.RootElement.GetProperty("info");
        doc.RootElement.GetProperty("openapi").GetString().Should().StartWith("3.");
        info.GetProperty("title").GetString().Should().Be("Uni+ — Módulo Organização Institucional");
        info.GetProperty("version").GetString().Should().Be("1.0.0");
    }

    [Fact(DisplayName = "Spec runtime de Organização bate com baseline committed em contracts/")]
    public async Task SpecRuntime_DeveCasarComBaselineCommitted()
    {
        using HttpClient client = _factory.CreateClient();

        await ContratoOpenApiGravado.ConferirAsync(client, RotaDaEspecificacao, CaminhoDoContratoGravado);
    }

    [Fact(DisplayName = "Contrato de Unidade não anuncia o enum de procedência do registro")]
    public async Task SpecRuntime_NaoDeclaraOEnumDeProcedenciaDaUnidade()
    {
        using JsonDocument especificacao = await ObterEspecificacaoAsync();

        SchemasDe(especificacao).TryGetProperty("OrigemUnidade", out _).Should().BeFalse(
            "o vocabulário de procedência foi removido do domínio da Unidade");
    }

    [Theory(DisplayName = "Nenhum schema de Unidade declara nem exige a propriedade origem")]
    [InlineData("CriarUnidadeCommand")]
    [InlineData("AtualizarUnidadeCommand")]
    [InlineData("UnidadeDto")]
    public async Task SpecRuntime_SchemaDeUnidadeNaoDeclaraOrigem(string nomeDoSchema)
    {
        using JsonDocument especificacao = await ObterEspecificacaoAsync();
        JsonElement schema = SchemasDe(especificacao).GetProperty(nomeDoSchema);

        schema.GetProperty("properties").TryGetProperty("origem", out _).Should().BeFalse();
        ObrigatoriosDe(schema).Should().NotContain("origem");
    }

    private async Task<JsonDocument> ObterEspecificacaoAsync()
    {
        using HttpClient client = _factory.CreateClient();
        using HttpResponseMessage response = await client.GetAsync(new Uri(RotaDaEspecificacao, UriKind.Relative));
        response.EnsureSuccessStatusCode();

        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static JsonElement SchemasDe(JsonDocument especificacao) =>
        especificacao.RootElement.GetProperty("components").GetProperty("schemas");

    private static IEnumerable<string?> ObrigatoriosDe(JsonElement schema) =>
        schema.TryGetProperty("required", out JsonElement obrigatorios)
            ? obrigatorios.EnumerateArray().Select(campo => campo.GetString())
            : [];
}
