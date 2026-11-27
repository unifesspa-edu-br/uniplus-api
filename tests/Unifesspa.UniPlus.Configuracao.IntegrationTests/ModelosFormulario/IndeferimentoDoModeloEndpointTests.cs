namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.ModelosFormulario;

using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.ModelosFormulario.DocumentosDoModeloParaTeste;
using static Unifesspa.UniPlus.Configuracao.IntegrationTests.ModelosFormulario.ModeloFormularioHttp;
using static Unifesspa.UniPlus.Configuracao.IntegrationTests.ModelosFormulario.ModelosParaTeste;

/// <summary>
/// O indeferimento do documento e do grupo do modelo de formulário: dizer se é obrigatório e se abre prazo
/// para reenvio é exigido, e falso dito é aceito, gravado e devolvido.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class IndeferimentoDoModeloEndpointTests
{
    private const string CodigoDaRecusa = "uniplus.configuracao.modelo_formulario.entrada_malformada";
    private const string Documentos = "conteudo.conjuntoDeDocumentos.documentosExigidos[0].indeferimento";
    private const string Grupos = "conteudo.conjuntoDeDocumentos.gruposDeDocumentos[0].indeferimento";

    private readonly ConfiguracaoEndpointFixture _fixture;

    public IndeferimentoDoModeloEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory(DisplayName = "O documento sem dizer se é obrigatório é recusado no campo dele e o modelo não é gravado")]
    [InlineData("omitido")]
    [InlineData("nulo")]
    public async Task Criar_DocumentoSemObrigatorio_RecusaENaoGrava(string modo)
    {
        JsonObject documento = Documento();
        Informar(documento["indeferimento"]!.AsObject(), "obrigatorio", modo);

        await ConferirRecusaDoCadastroAsync(ModeloComDocumento(documento), $"{Documentos}.obrigatorio");
    }

    [Theory(DisplayName = "O documento sem dizer se abre prazo para reenvio é recusado no campo dele e o modelo não é gravado")]
    [InlineData("omitido")]
    [InlineData("nulo")]
    public async Task Criar_DocumentoSemReenvio_RecusaENaoGrava(string modo)
    {
        JsonObject documento = Documento();
        Informar(documento["indeferimento"]!.AsObject(), "abrePrazoParaReenvio", modo);

        await ConferirRecusaDoCadastroAsync(ModeloComDocumento(documento), $"{Documentos}.abrePrazoParaReenvio");
    }

    [Fact(DisplayName = "O documento sem o bloco de indeferimento é recusado nos dois campos")]
    public async Task Criar_DocumentoSemIndeferimento_RecusaOsDoisCampos()
    {
        JsonObject documento = Documento();
        documento["indeferimento"] = null;

        await ConferirRecusaDoCadastroAsync(ModeloComDocumento(documento), $"{Documentos}.obrigatorio", $"{Documentos}.abrePrazoParaReenvio");
    }

    [Fact(DisplayName = "O documento com falso dito nos dois campos é gravado e devolvido com falso")]
    public async Task Criar_DocumentoComFalsoDito_GravaEDevolveFalso()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        JsonObject documento = Documento();
        documento["indeferimento"]!["obrigatorio"] = false;

        Guid id = await CriarAsync(client, ModeloComDocumento(documento));

        JsonNode lido = (await ObterAsync(client, id))["conteudo"]!["conjuntoDeDocumentos"]!["documentosExigidos"]![0]!["indeferimento"]!;
        (lido["obrigatorio"]!.GetValue<bool>(), lido["abrePrazoParaReenvio"]!.GetValue<bool>()).Should().Be((false, false));
    }

    [Theory(DisplayName = "O grupo com documentos sem dizer se abre prazo para reenvio é recusado no campo dele e o modelo não é gravado")]
    [InlineData("omitido")]
    [InlineData("nulo")]
    public async Task Criar_GrupoSemReenvio_RecusaENaoGrava(string modo)
    {
        JsonObject grupo = GrupoDeDocumentos();
        Informar(grupo["indeferimento"]!.AsObject(), "abrePrazoParaReenvio", modo);

        await ConferirRecusaDoCadastroAsync(ModeloComGrupo(grupo), $"{Grupos}.abrePrazoParaReenvio");
    }

    [Fact(DisplayName = "O grupo com falso dito é gravado e devolvido com falso, e o grupo sem indeferimento continua aceito")]
    public async Task Criar_GrupoComFalsoDitoESemIndeferimento_Grava()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        JsonObject semIndeferimento = GrupoDeDocumentos();
        semIndeferimento["indeferimento"] = null;

        Guid id = await CriarAsync(client, ModeloComGrupo(GrupoDeDocumentos(), semIndeferimento));

        JsonNode grupos = (await ObterAsync(client, id))["conteudo"]!["conjuntoDeDocumentos"]!["gruposDeDocumentos"]!;
        grupos[0]!["indeferimento"]!["abrePrazoParaReenvio"]!.GetValue<bool>().Should().BeFalse();
        grupos[1]!["indeferimento"].Should().BeNull();
    }

    [Fact(DisplayName = "A edição com o documento sem dizer se é obrigatório é recusada e o modelo segue como estava")]
    public async Task Atualizar_DocumentoSemObrigatorio_RecusaEMantemOModelo()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CriarAsync(client, ModeloComDocumento(Documento()));
        JsonObject conteudo = (await ObterAsync(client, id))["conteudo"]!.AsObject();
        JsonObject semObrigatorio = Documento();
        semObrigatorio["indeferimento"]!.AsObject().Remove("obrigatorio");
        conteudo["conjuntoDeDocumentos"] = ConjuntoCom(new JsonArray(semObrigatorio));

        HttpResponseMessage resposta = await EnviarAsync(client, HttpMethod.Put, $"{Base}/{id}", Edicao("Habilitação", conteudo));

        (await RecusasAsync(resposta)).Should().Equal(($"{Documentos}.obrigatorio", CodigoDaRecusa));
        JsonNode mantido = (await ObterAsync(client, id))["conteudo"]!["conjuntoDeDocumentos"]!["documentosExigidos"]!.AsArray().Should().ContainSingle().Subject!;
        mantido["indeferimento"]!["obrigatorio"]!.GetValue<bool>().Should().BeTrue();
    }

    private static JsonObject ModeloComDocumento(JsonObject documento)
    {
        JsonObject modelo = JsonSerializer.SerializeToNode(Habilitacao(CodigoUnico()))!.AsObject();
        modelo["conteudo"]!["conjuntoDeDocumentos"] = ConjuntoCom(new JsonArray(documento));
        return modelo;
    }

    private static JsonObject ModeloComGrupo(params JsonObject[] grupos)
    {
        JsonObject modelo = JsonSerializer.SerializeToNode(Habilitacao(CodigoUnico()))!.AsObject();
        modelo["conteudo"]!["conjuntoDeDocumentos"] = ConjuntoCom(new JsonArray(), new JsonArray([.. grupos]));
        return modelo;
    }

    private static void Informar(JsonObject indeferimento, string campo, string modo)
    {
        if (modo == "omitido")
        {
            indeferimento.Remove(campo);
            return;
        }

        indeferimento[campo] = null;
    }

    private async Task ConferirRecusaDoCadastroAsync(JsonObject modelo, params string[] campos)
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage resposta = await EnviarAsync(client, HttpMethod.Post, Base, modelo);

        (await RecusasAsync(resposta)).Should().Equal(campos.Select(static c => ((string?)c, (string?)CodigoDaRecusa)));
        using JsonDocument lista = await ListarAsync(client, "?serveAFaseDeInscricao=false");
        lista.RootElement.EnumerateArray().Select(static m => m.GetProperty("codigo").GetString())
            .Should().NotContain(modelo["codigo"]!.GetValue<string>());
    }

    private static async Task<IReadOnlyList<(string?, string?)>> RecusasAsync(HttpResponseMessage resposta)
    {
        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, corpo);
        using JsonDocument problema = JsonDocument.Parse(corpo);
        return [.. problema.RootElement.GetProperty("errors").EnumerateArray()
            .Select(static e => (e.GetProperty("field").GetString(), e.GetProperty("code").GetString()))];
    }
}
