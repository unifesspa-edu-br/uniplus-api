namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

public sealed partial class IndicadoresDoProcessoEndpointTests
{
    [Fact(DisplayName = "OpenAPI documenta o sub-recurso, vendor media type e respostas HTTP do contrato")]
    public async Task OpenApi_DeclaraContratoDosIndicadores()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpResponseMessage response = await client.GetAsync("/openapi/selecao.json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement operation = document.RootElement.GetProperty("paths")
            .GetProperty("/api/selecao/processos-seletivos/{id}/indicadores").GetProperty("get");
        JsonElement idParameter = operation.GetProperty("parameters").EnumerateArray()
            .Single(p => p.GetProperty("name").GetString() == "id");
        idParameter.GetProperty("in").GetString().Should().Be("path");
        idParameter.GetProperty("required").GetBoolean().Should().BeTrue();
        Esquema(idParameter.GetProperty("schema"), "string", "uuid").Should().BeTrue();
        JsonElement responses = operation.GetProperty("responses");
        responses.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(["200", "401", "403", "404", "406"]);
        JsonElement sucesso = responses.GetProperty("200").GetProperty("content");
        sucesso.EnumerateObject().Select(p => p.Name).Should().Equal(MediaType);
        JsonElement schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        JsonElement responseSchema = ResolverSchema(schemas, sucesso.GetProperty(MediaType).GetProperty("schema"));
        JsonElement properties = responseSchema.GetProperty("properties");
        Propriedades(properties).Should().BeEquivalentTo(["processoSeletivoId", "consultadoEm", "indicadores", "_links"]);
        Obrigatorias(responseSchema).Should().BeEquivalentTo(["processoSeletivoId", "consultadoEm", "indicadores", "_links"]);
        Esquema(properties.GetProperty("processoSeletivoId"), "string", "uuid").Should().BeTrue();
        Esquema(properties.GetProperty("consultadoEm"), "string", "date-time").Should().BeTrue();

        JsonElement lista = properties.GetProperty("indicadores");
        lista.GetProperty("type").GetString().Should().Be("array");
        JsonElement itemSchema = ResolverSchema(schemas, lista.GetProperty("items"));
        JsonElement itemProperties = itemSchema.GetProperty("properties");
        Propriedades(itemProperties).Should().BeEquivalentTo(["codigo", "valor", "label", "descricao"]);
        Obrigatorias(itemSchema).Should().BeEquivalentTo(["codigo", "valor", "label", "descricao"]);
        JsonElement codigoSchema = ResolverSchema(schemas, itemProperties.GetProperty("codigo"));
        codigoSchema.GetProperty("enum").EnumerateArray().Select(v => v.GetString()).Should()
            .BeEquivalentTo(["INSCRICOES", "INSCRITOS", "RASCUNHOS"]);
        Esquema(itemProperties.GetProperty("valor"), "integer", "int64").Should().BeTrue();
        Esquema(itemProperties.GetProperty("label"), "string").Should().BeTrue();
        Esquema(itemProperties.GetProperty("descricao"), "string").Should().BeTrue();

        JsonElement linksSchema = ResolverSchema(schemas, properties.GetProperty("_links"));
        JsonElement linkProperties = linksSchema.GetProperty("properties");
        Propriedades(linkProperties).Should().BeEquivalentTo(["self", "processo"]);
        Obrigatorias(linksSchema).Should().BeEquivalentTo(["self", "processo"]);
        Esquema(linkProperties.GetProperty("self"), "string").Should().BeTrue();
        Esquema(linkProperties.GetProperty("processo"), "string").Should().BeTrue();

        foreach (string status in new[] { "401", "403", "404", "406" })
        {
            JsonElement erro = responses.GetProperty(status);
            erro.GetProperty("content").TryGetProperty("application/problem+json", out JsonElement problem)
                .Should().BeTrue($"a resposta {status} deve documentar Problem Details");
            JsonElement problemaSchema = ResolverSchema(schemas, problem.GetProperty("schema"));
            Propriedades(problemaSchema.GetProperty("properties"))
                .Should().Contain(["type", "title", "status", "detail"]);
        }
    }
}
