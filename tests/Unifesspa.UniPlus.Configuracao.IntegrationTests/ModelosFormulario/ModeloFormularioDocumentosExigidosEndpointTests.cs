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

    [Fact(DisplayName = "A situação da base legal é gravada e devolvida no modelo")]
    public async Task Criar_BaseLegalComSituacao_GravaEDevolveSituacao()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        JsonObject modelo = JsonSerializer.SerializeToNode(Habilitacao(CodigoUnico()))!.AsObject();
        JsonObject documento = Documento();
        documento["indeferimento"]!["basesLegais"] = JsonNode.Parse("""[{"referencia":"Lei de referência","abrangencia":"FEDERAL","situacao":"RESOLVIDO"}]""");
        modelo["conteudo"]!["conjuntoDeDocumentos"] = ConjuntoCom(new JsonArray(documento));

        Guid id = await CriarAsync(client, modelo);
        JsonObject baseLegal = (await ObterAsync(client, id))["conteudo"]!["conjuntoDeDocumentos"]!["documentosExigidos"]![0]!["indeferimento"]!["basesLegais"]![0]!.AsObject();

        baseLegal["situacao"]!.GetValue<string>().Should().Be("RESOLVIDO");
        baseLegal.ContainsKey("status").Should().BeFalse();
    }

    [Fact(DisplayName = "O prazo de emissão escolhido é gravado e devolvido com código e rótulo, e a validade não escolhida volta nula")]
    public async Task Criar_PrazoDeEmissaoEscolhido_DevolveCodigoERotulo()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        JsonObject modelo = JsonSerializer.SerializeToNode(Habilitacao(CodigoUnico()))!.AsObject();
        JsonObject documento = Documento();
        documento["requisitos"] = RequisitosComPrazo("COM_PRAZO");
        modelo["conteudo"]!["conjuntoDeDocumentos"] = ConjuntoCom(new JsonArray(documento));

        Guid id = await CriarAsync(client, modelo);
        JsonNode requisitos = (await ObterAsync(client, id))["conteudo"]!["conjuntoDeDocumentos"]!["documentosExigidos"]![0]!["requisitos"]!;

        requisitos["prazoDeEmissao"]!["escolha"]!["codigo"]!.GetValue<string>().Should().Be("COM_PRAZO");
        requisitos["prazoDeEmissao"]!["escolha"]!["rotulo"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace();
        requisitos["prazoDeEmissao"]!["referencia"]!["faseCodigo"]!.GetValue<string>().Should().Be("HABILITACAO");
        requisitos["validade"].Should().BeNull();
    }

    [Fact(DisplayName = "O código de prazo de emissão desconhecido é recusado no campo dele, na posição do documento")]
    public async Task Criar_CodigoDeRequisitoDesconhecido_RecusaNoCampoDoRequisito()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        JsonObject documento = Documento();
        documento["requisitos"] = RequisitosComPrazo("naoExiste");

        IReadOnlyList<(string?, string?)> recusas = await RecusasDoCadastroAsync(client, [documento]);

        recusas.Select(static r => r.Item1).Should().Contain("conteudo.conjuntoDeDocumentos.documentosExigidos[0].requisitos.prazoDeEmissao.escolha");
    }

    [Fact(DisplayName = "A recusa do catálogo aponta a posição do documento na entrada, com um documento sem fase antes dele")]
    public async Task Criar_DocumentoSemFaseAntes_RecusaDoCatalogoApontaAPosicaoDaEntrada()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        JsonNode[] documentos = [Documento(faseCodigo: ""), Documento(quando: CondicaoForaDoCatalogo())];

        IReadOnlyList<(string?, string?)> recusas = await RecusasDoCadastroAsync(client, documentos);

        recusas.Select(static r => r.Item1).Should().Contain(
            ["conteudo.conjuntoDeDocumentos.documentosExigidos[0].faseCodigo", "conteudo.conjuntoDeDocumentos.documentosExigidos[1].quando"]);
    }

    [Fact(DisplayName = "A recusa da exigência por opção aponta a posição do documento na entrada, com um documento nulo antes dele")]
    public async Task Criar_DocumentoNuloAntes_RecusaPorOpcaoApontaAPosicaoDaEntrada()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        JsonNode?[] documentos = [null, Documento(porOpcaoDoFato: FatoForaDoCatalogo)];

        IReadOnlyList<(string?, string?)> recusas = await RecusasDoCadastroAsync(client, documentos);

        recusas.Select(static r => r.Item1).Should().Contain("conteudo.conjuntoDeDocumentos.documentosExigidos[1].porOpcao.fatoCodigo");
    }

    [Fact(DisplayName = "Sem documento malformado a recusa aponta a posição do documento na entrada")]
    public async Task Criar_DocumentosBemFormados_RecusaApontaAPosicaoDaEntrada()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        JsonNode[] documentos = [Documento(), Documento(quando: CondicaoForaDoCatalogo())];

        IReadOnlyList<(string?, string?)> recusas = await RecusasDoCadastroAsync(client, documentos);

        recusas.Select(static r => r.Item1).Should().Contain("conteudo.conjuntoDeDocumentos.documentosExigidos[1].quando")
            .And.NotContain("conteudo.conjuntoDeDocumentos.documentosExigidos[0].quando");
    }

    [Fact(DisplayName = "O grupo de documentos é gravado e devolvido com a quantidade, o indeferimento e os documentos dele")]
    public async Task Criar_GrupoDeDocumentos_GravaEDevolveOGrupo()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        JsonObject modelo = JsonSerializer.SerializeToNode(Habilitacao(CodigoUnico()))!.AsObject();
        modelo["conteudo"]!["conjuntoDeDocumentos"] = ConjuntoCom(new JsonArray(), new JsonArray(GrupoDeDocumentos()));

        Guid id = await CriarAsync(client, modelo);
        JsonNode devolvido = (await ObterAsync(client, id))["conteudo"]!["conjuntoDeDocumentos"]!["gruposDeDocumentos"]![0]!;

        devolvido["quantidadeMinima"]!.GetValue<int>().Should().Be(1);
        devolvido["indeferimento"]!["consequencia"]!.GetValue<string>().Should().Be("ELIMINA");
        devolvido["documentos"]!.AsArray().Should().ContainSingle();
    }

    [Fact(DisplayName = "O documento de um grupo com fato fora do catálogo é recusado no caminho do documento do grupo")]
    public async Task Criar_DocumentoDeGrupoForaDoCatalogo_RecusaNoCaminhoDoGrupo()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        JsonObject modelo = JsonSerializer.SerializeToNode(Habilitacao(CodigoUnico()))!.AsObject();
        JsonObject grupo = new() { ["quantidadeMinima"] = null, ["indeferimento"] = null, ["documentos"] = new JsonArray(Documento(quando: CondicaoForaDoCatalogo())) };
        modelo["conteudo"]!["conjuntoDeDocumentos"] = ConjuntoCom(new JsonArray(), new JsonArray(grupo));

        HttpResponseMessage resposta = await EnviarAsync(client, HttpMethod.Post, Base, modelo);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CamposRecusadosAsync(resposta)).Should().Contain("conteudo.conjuntoDeDocumentos.gruposDeDocumentos[0].documentos[0].quando");
    }

    [Fact(DisplayName = "A edição recusa com a posição do documento na entrada, com um documento sem fase antes dele")]
    public async Task Atualizar_DocumentoSemFaseAntes_RecusaDoCatalogoApontaAPosicaoDaEntrada()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CriarAsync(client, Habilitacao(CodigoUnico()));
        JsonObject conteudo = (await ObterAsync(client, id))["conteudo"]!.AsObject();
        conteudo["conjuntoDeDocumentos"] = ConjuntoCom(new JsonArray(Documento(faseCodigo: ""), Documento(quando: CondicaoForaDoCatalogo())));

        HttpResponseMessage resposta = await EnviarAsync(client, HttpMethod.Put, $"{Base}/{id}", Edicao("Habilitação", conteudo));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CamposRecusadosAsync(resposta)).Should().Contain("conteudo.conjuntoDeDocumentos.documentosExigidos[1].quando");
    }

    private static async Task<IReadOnlyList<(string?, string?)>> RecusasDoCadastroAsync(HttpClient client, IEnumerable<JsonNode?> documentos)
    {
        JsonObject modelo = JsonSerializer.SerializeToNode(Habilitacao(CodigoUnico()))!.AsObject();
        modelo["conteudo"]!["conjuntoDeDocumentos"] = ConjuntoCom(new JsonArray([.. documentos]));

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

    private static JsonObject GrupoDeDocumentos() => new()
    {
        ["quantidadeMinima"] = 1,
        ["indeferimento"] = new JsonObject { ["consequencia"] = "ELIMINA", ["basesLegais"] = new JsonArray() },
        ["documentos"] = new JsonArray(Documento()),
    };

    private static JsonObject ConjuntoCom(JsonArray documentos, JsonArray? grupos = null) =>
        new() { ["documentosExigidos"] = documentos, ["gruposDeDocumentos"] = grupos ?? new JsonArray() };

    private static JsonObject Documento(string? faseCodigo = "HABILITACAO", JsonNode? quando = null, string? porOpcaoDoFato = null) => new()
    {
        ["faseCodigo"] = faseCodigo,
        ["tipoDocumentoCodigo"] = "COMPROVANTE_RESIDENCIA",
        ["quando"] = quando,
        ["indeferimento"] = new JsonObject { ["obrigatorio"] = true, ["consequencia"] = "ELIMINA", ["basesLegais"] = new JsonArray() },
        ["porOpcao"] = porOpcaoDoFato is null ? null : new JsonObject { ["fatoCodigo"] = porOpcaoDoFato, ["exceto"] = new JsonArray() },
    };

    private static JsonObject RequisitosComPrazo(string escolha) => new()
    {
        ["prazoDeEmissao"] = new JsonObject
        {
            ["escolha"] = escolha,
            ["valor"] = 90,
            ["unidade"] = "DIAS",
            ["referencia"] = new JsonObject { ["tipo"] = "INICIO_DA_FASE", ["faseCodigo"] = "HABILITACAO" },
            ["efeitoForaDoPrazo"] = "RECUSAR_DOCUMENTO",
        },
    };

    private static JsonArray CondicaoForaDoCatalogo() =>
        new(new JsonArray(new JsonObject { ["fato"] = FatoForaDoCatalogo, ["operador"] = "IGUAL", ["valor"] = true }));
}
