namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.ModelosFormulario;

using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.ModelosFormulario.ModeloFormularioHttp;
using static Unifesspa.UniPlus.Configuracao.IntegrationTests.ModelosFormulario.ModelosParaTeste;

/// <summary>
/// As recusas dos documentos exigidos do modelo de formulário apontam a posição que o documento tem na
/// entrada, mesmo quando um documento anterior foi recusado pela leitura.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class ModeloFormularioDocumentosExigidosEndpointTests
{
    private const string FatoForaDoCatalogo = "FATO_FORA_DO_CATALOGO";

    private readonly ConfiguracaoEndpointFixture _fixture;

    public ModeloFormularioDocumentosExigidosEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "A recusa do catálogo aponta a posição do documento na entrada, com um documento sem fase antes dele")]
    public async Task Criar_DocumentoSemFaseAntes_RecusaDoCatalogoApontaAPosicaoDaEntrada()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        JsonNode[] documentos = [Documento(faseCodigo: ""), Documento(quando: CondicaoForaDoCatalogo())];

        IReadOnlyList<(string?, string?)> recusas = await RecusasDoCadastroAsync(client, documentos);

        recusas.Select(static r => r.Item1).Should().Contain(
            ["conteudo.documentosExigidos[0].faseCodigo", "conteudo.documentosExigidos[1].quando"]);
    }

    [Fact(DisplayName = "A recusa da exigência por opção aponta a posição do documento na entrada, com um documento nulo antes dele")]
    public async Task Criar_DocumentoNuloAntes_RecusaPorOpcaoApontaAPosicaoDaEntrada()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        JsonNode?[] documentos = [null, Documento(porOpcaoDoFato: FatoForaDoCatalogo)];

        IReadOnlyList<(string?, string?)> recusas = await RecusasDoCadastroAsync(client, documentos);

        recusas.Select(static r => r.Item1).Should().Contain("conteudo.documentosExigidos[1].porOpcao.fatoCodigo");
    }

    [Fact(DisplayName = "Sem documento malformado a recusa aponta a posição do documento na entrada")]
    public async Task Criar_DocumentosBemFormados_RecusaApontaAPosicaoDaEntrada()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        JsonNode[] documentos = [Documento(), Documento(quando: CondicaoForaDoCatalogo())];

        IReadOnlyList<(string?, string?)> recusas = await RecusasDoCadastroAsync(client, documentos);

        recusas.Select(static r => r.Item1).Should().Contain("conteudo.documentosExigidos[1].quando")
            .And.NotContain("conteudo.documentosExigidos[0].quando");
    }

    [Fact(DisplayName = "A edição recusa com a posição do documento na entrada, com um documento sem fase antes dele")]
    public async Task Atualizar_DocumentoSemFaseAntes_RecusaDoCatalogoApontaAPosicaoDaEntrada()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CriarAsync(client, Habilitacao(CodigoUnico()));
        JsonObject conteudo = (await ObterAsync(client, id))["conteudo"]!.AsObject();
        conteudo["documentosExigidos"] = new JsonArray(Documento(faseCodigo: ""), Documento(quando: CondicaoForaDoCatalogo()));

        HttpResponseMessage resposta = await EnviarAsync(client, HttpMethod.Put, $"{Base}/{id}", Edicao("Habilitação", conteudo));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CamposRecusadosAsync(resposta)).Should().Contain("conteudo.documentosExigidos[1].quando");
    }

    private static async Task<IReadOnlyList<(string?, string?)>> RecusasDoCadastroAsync(HttpClient client, IEnumerable<JsonNode?> documentos)
    {
        JsonObject modelo = JsonSerializer.SerializeToNode(Habilitacao(CodigoUnico()))!.AsObject();
        modelo["conteudo"]!["documentosExigidos"] = new JsonArray([.. documentos]);

        HttpResponseMessage resposta = await EnviarAsync(client, HttpMethod.Post, Base, modelo);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await resposta.Content.ReadAsStringAsync());
        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        return [.. problema.RootElement.GetProperty("errors").EnumerateArray()
            .Select(static e => (e.GetProperty("field").GetString(), e.GetProperty("code").GetString()))];
    }

    private static async Task<IReadOnlyList<string?>> CamposRecusadosAsync(HttpResponseMessage resposta)
    {
        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        return [.. problema.RootElement.GetProperty("errors").EnumerateArray().Select(static e => e.GetProperty("field").GetString())];
    }

    private static JsonObject Documento(string? faseCodigo = "HABILITACAO", JsonNode? quando = null, string? porOpcaoDoFato = null) => new()
    {
        ["faseCodigo"] = faseCodigo,
        ["tipoDocumentoCodigo"] = "COMPROVANTE_RESIDENCIA",
        ["obrigatorio"] = true,
        ["quando"] = quando,
        ["consequenciaIndeferimento"] = "INDEFERE",
        ["basesLegais"] = new JsonArray(),
        ["porOpcao"] = porOpcaoDoFato is null ? null : new JsonObject { ["fatoCodigo"] = porOpcaoDoFato, ["exceto"] = new JsonArray() },
    };

    private static JsonArray CondicaoForaDoCatalogo() =>
        new(new JsonArray(new JsonObject { ["fato"] = FatoForaDoCatalogo, ["operador"] = "IGUAL", ["valor"] = true }));
}
