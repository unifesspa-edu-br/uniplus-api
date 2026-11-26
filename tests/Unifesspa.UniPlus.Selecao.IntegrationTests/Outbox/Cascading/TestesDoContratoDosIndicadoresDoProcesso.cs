namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.ApoioDoCenarioDeIndicadores;

[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class TestesDoContratoDosIndicadoresDoProcesso : IAsyncLifetime
{
    private readonly ApoioDoCenarioDeIndicadores _apoio;

    public TestesDoContratoDosIndicadoresDoProcesso(CascadingFixture cenario) => _apoio = new ApoioDoCenarioDeIndicadores(cenario);

    public Task InitializeAsync() => _apoio.LimparConcessoesAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact(DisplayName = "OpenAPI documenta o sub-recurso, metadados, tipo de mídia e respostas HTTP")]
    public async Task OpenApi_DeclaraContratoDosIndicadores()
    {
        using JsonDocument documento = await ObterDocumentoOpenApiAsync();
        JsonElement operacao = ObterOperacao(documento);

        ConferirMetadados(operacao);
        ConferirParametroERespostas(operacao);
        ConferirSchemaDeSucesso(documento, operacao);
        ConferirProblemDetails(documento, operacao);
    }

    private async Task<JsonDocument> ObterDocumentoOpenApiAsync()
    {
        using HttpResponseMessage resposta = await _apoio.EnviarAsync(null, "/openapi/selecao.json", null);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
    }

    private static JsonElement ObterOperacao(JsonDocument documento) =>
        documento.RootElement.GetProperty("paths")
            .GetProperty("/api/selecao/processos-seletivos/{id}/indicadores").GetProperty("get");

    private static void ConferirMetadados(JsonElement operacao)
    {
        operacao.GetProperty("operationId").GetString().Should().Be("ObterIndicadoresDoProcesso");
        operacao.GetProperty("summary").GetString().Should().Be("Obtém os indicadores quantitativos do processo seletivo");
        operacao.GetProperty("description").GetString().Should()
            .Contain("inscrições enviadas não canceladas, inscritos distintos e rascunhos");
    }

    private static void ConferirParametroERespostas(JsonElement operacao)
    {
        JsonElement id = operacao.GetProperty("parameters").EnumerateArray()
            .Single(parametro => parametro.GetProperty("name").GetString() == "id");
        id.GetProperty("in").GetString().Should().Be("path");
        id.GetProperty("required").GetBoolean().Should().BeTrue();
        Esquema(id.GetProperty("schema"), "string", "uuid").Should().BeTrue();
        JsonElement respostas = operacao.GetProperty("responses");
        respostas.EnumerateObject().Select(resposta => resposta.Name)
            .Should().BeEquivalentTo(["200", "401", "403", "404", "406"]);
        respostas.GetProperty("200").GetProperty("content").EnumerateObject()
            .Select(conteudo => conteudo.Name).Should().Equal(MediaType);
    }

    private static void ConferirSchemaDeSucesso(JsonDocument documento, JsonElement operacao)
    {
        JsonElement esquemas = documento.RootElement.GetProperty("components").GetProperty("schemas");
        JsonElement conteudo = operacao.GetProperty("responses").GetProperty("200").GetProperty("content");
        JsonElement esquemaDaResposta = ResolverEsquema(esquemas, conteudo.GetProperty(MediaType).GetProperty("schema"));
        JsonElement propriedades = esquemaDaResposta.GetProperty("properties");
        Propriedades(propriedades).Should().BeEquivalentTo(["processoSeletivoId", "consultadoEm", "indicadores", "_links"]);
        Obrigatorias(esquemaDaResposta).Should().BeEquivalentTo(["processoSeletivoId", "consultadoEm", "indicadores", "_links"]);
        Esquema(propriedades.GetProperty("processoSeletivoId"), "string", "uuid").Should().BeTrue();
        Esquema(propriedades.GetProperty("consultadoEm"), "string", "date-time").Should().BeTrue();
        ConferirIndicadores(esquemas, propriedades.GetProperty("indicadores"));
        ConferirLinks(esquemas, propriedades.GetProperty("_links"));
    }

    private static void ConferirIndicadores(JsonElement esquemas, JsonElement lista)
    {
        lista.GetProperty("type").GetString().Should().Be("array");
        JsonElement item = ResolverEsquema(esquemas, lista.GetProperty("items"));
        JsonElement propriedades = item.GetProperty("properties");
        Propriedades(propriedades).Should().BeEquivalentTo(["codigo", "valor", "rotulo", "descricao"]);
        Obrigatorias(item).Should().BeEquivalentTo(["codigo", "valor", "rotulo", "descricao"]);
        ResolverEsquema(esquemas, propriedades.GetProperty("codigo")).GetProperty("enum").EnumerateArray()
            .Select(valor => valor.GetString()).Should().BeEquivalentTo(["INSCRICOES", "INSCRITOS", "RASCUNHOS"]);
        Esquema(propriedades.GetProperty("valor"), "integer", "int64").Should().BeTrue();
        Esquema(propriedades.GetProperty("rotulo"), "string").Should().BeTrue();
        Esquema(propriedades.GetProperty("descricao"), "string").Should().BeTrue();
    }

    private static void ConferirLinks(JsonElement esquemas, JsonElement links)
    {
        JsonElement esquema = ResolverEsquema(esquemas, links);
        JsonElement propriedades = esquema.GetProperty("properties");
        Propriedades(propriedades).Should().BeEquivalentTo(["self", "processo"]);
        Obrigatorias(esquema).Should().BeEquivalentTo(["self", "processo"]);
        Esquema(propriedades.GetProperty("self"), "string").Should().BeTrue();
        Esquema(propriedades.GetProperty("processo"), "string").Should().BeTrue();
    }

    private static void ConferirProblemDetails(JsonDocument documento, JsonElement operacao)
    {
        JsonElement esquemas = documento.RootElement.GetProperty("components").GetProperty("schemas");
        foreach (string situacao in new[] { "401", "403", "404", "406" })
        {
            JsonElement resposta = operacao.GetProperty("responses").GetProperty(situacao);
            resposta.GetProperty("content").TryGetProperty("application/problem+json", out JsonElement problema)
                .Should().BeTrue($"a resposta {situacao} deve documentar Problem Details");
            JsonElement esquema = ResolverEsquema(esquemas, problema.GetProperty("schema"));
            Propriedades(esquema.GetProperty("properties")).Should().Contain(["type", "title", "status", "detail"]);
        }
    }
    private static string[] Propriedades(JsonElement objeto) =>
        objeto.EnumerateObject().Select(p => p.Name).ToArray();

    private static string[] Obrigatorias(JsonElement esquema) =>
        esquema.GetProperty("required").EnumerateArray().Select(p => p.GetString()!).ToArray();

    private static JsonElement ResolverEsquema(JsonElement esquemas, JsonElement esquema) =>
        esquema.TryGetProperty("$ref", out JsonElement referencia)
            ? esquemas.GetProperty(referencia.GetString()!.Split('/').Last())
            : esquema;

    private static bool Esquema(JsonElement esquema, string tipo, string? formato = null)
    {
        JsonElement resolvido = esquema;
        if (esquema.TryGetProperty("$ref", out JsonElement referencia))
            return false;

        return resolvido.GetProperty("type").GetString() == tipo
            && (formato is null || resolvido.GetProperty("format").GetString() == formato);
    }

}
