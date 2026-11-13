namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

public sealed partial class IndicadoresDoProcessoEndpointTests
{
    [Fact(DisplayName = "OpenAPI documenta o sub-recurso, metadados, vendor media type e respostas HTTP")]
    public async Task OpenApi_DeclaraContratoDosIndicadores()
    {
        using JsonDocument document = await ObterDocumentoOpenApiAsync();
        JsonElement operation = ObterOperacao(document);

        ConferirMetadados(operation);
        ConferirParametroERespostas(operation);
        ConferirSchemaDeSucesso(document, operation);
        ConferirProblemDetails(document, operation);
    }

    private async Task<JsonDocument> ObterDocumentoOpenApiAsync()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpResponseMessage response = await client.GetAsync("/openapi/selecao.json");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static JsonElement ObterOperacao(JsonDocument document) =>
        document.RootElement.GetProperty("paths")
            .GetProperty("/api/selecao/processos-seletivos/{id}/indicadores").GetProperty("get");

    private static void ConferirMetadados(JsonElement operation)
    {
        operation.GetProperty("operationId").GetString().Should().Be("ObterIndicadoresDoProcesso");
        operation.GetProperty("summary").GetString().Should().Be("Obtém os indicadores quantitativos do processo seletivo");
        operation.GetProperty("description").GetString().Should()
            .Contain("inscrições submetidas não canceladas, inscritos distintos e rascunhos");
    }

    private static void ConferirParametroERespostas(JsonElement operation)
    {
        JsonElement id = operation.GetProperty("parameters").EnumerateArray()
            .Single(parameter => parameter.GetProperty("name").GetString() == "id");
        id.GetProperty("in").GetString().Should().Be("path");
        id.GetProperty("required").GetBoolean().Should().BeTrue();
        Esquema(id.GetProperty("schema"), "string", "uuid").Should().BeTrue();
        JsonElement responses = operation.GetProperty("responses");
        responses.EnumerateObject().Select(response => response.Name)
            .Should().BeEquivalentTo(["200", "401", "403", "404", "406"]);
        responses.GetProperty("200").GetProperty("content").EnumerateObject()
            .Select(content => content.Name).Should().Equal(MediaType);
    }

    private static void ConferirSchemaDeSucesso(JsonDocument document, JsonElement operation)
    {
        JsonElement schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        JsonElement content = operation.GetProperty("responses").GetProperty("200").GetProperty("content");
        JsonElement responseSchema = ResolverSchema(schemas, content.GetProperty(MediaType).GetProperty("schema"));
        JsonElement properties = responseSchema.GetProperty("properties");
        Propriedades(properties).Should().BeEquivalentTo(["processoSeletivoId", "consultadoEm", "indicadores", "_links"]);
        Obrigatorias(responseSchema).Should().BeEquivalentTo(["processoSeletivoId", "consultadoEm", "indicadores", "_links"]);
        Esquema(properties.GetProperty("processoSeletivoId"), "string", "uuid").Should().BeTrue();
        Esquema(properties.GetProperty("consultadoEm"), "string", "date-time").Should().BeTrue();
        ConferirIndicadores(schemas, properties.GetProperty("indicadores"));
        ConferirLinks(schemas, properties.GetProperty("_links"));
    }

    private static void ConferirIndicadores(JsonElement schemas, JsonElement lista)
    {
        lista.GetProperty("type").GetString().Should().Be("array");
        JsonElement item = ResolverSchema(schemas, lista.GetProperty("items"));
        JsonElement properties = item.GetProperty("properties");
        Propriedades(properties).Should().BeEquivalentTo(["codigo", "valor", "label", "descricao"]);
        Obrigatorias(item).Should().BeEquivalentTo(["codigo", "valor", "label", "descricao"]);
        ResolverSchema(schemas, properties.GetProperty("codigo")).GetProperty("enum").EnumerateArray()
            .Select(value => value.GetString()).Should().BeEquivalentTo(["INSCRICOES", "INSCRITOS", "RASCUNHOS"]);
        Esquema(properties.GetProperty("valor"), "integer", "int64").Should().BeTrue();
        Esquema(properties.GetProperty("label"), "string").Should().BeTrue();
        Esquema(properties.GetProperty("descricao"), "string").Should().BeTrue();
    }

    private static void ConferirLinks(JsonElement schemas, JsonElement links)
    {
        JsonElement schema = ResolverSchema(schemas, links);
        JsonElement properties = schema.GetProperty("properties");
        Propriedades(properties).Should().BeEquivalentTo(["self", "processo"]);
        Obrigatorias(schema).Should().BeEquivalentTo(["self", "processo"]);
        Esquema(properties.GetProperty("self"), "string").Should().BeTrue();
        Esquema(properties.GetProperty("processo"), "string").Should().BeTrue();
    }

    private static void ConferirProblemDetails(JsonDocument document, JsonElement operation)
    {
        JsonElement schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        foreach (string status in new[] { "401", "403", "404", "406" })
        {
            JsonElement response = operation.GetProperty("responses").GetProperty(status);
            response.GetProperty("content").TryGetProperty("application/problem+json", out JsonElement problem)
                .Should().BeTrue($"a resposta {status} deve documentar Problem Details");
            JsonElement schema = ResolverSchema(schemas, problem.GetProperty("schema"));
            Propriedades(schema.GetProperty("properties")).Should().Contain(["type", "title", "status", "detail"]);
        }
    }
}
