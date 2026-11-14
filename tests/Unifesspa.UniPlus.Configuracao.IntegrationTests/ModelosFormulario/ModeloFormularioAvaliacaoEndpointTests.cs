namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.ModelosFormulario;

using System.Net;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.ModelosFormulario.ModeloFormularioHttp;
using static Unifesspa.UniPlus.Configuracao.IntegrationTests.ModelosFormulario.ModelosParaTeste;

/// <summary>
/// A avaliação das regras de um modelo sem cadastro: o mesmo resultado da pré-visualização do modelo, e a
/// recusa de regras que não formam formulário.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class ModeloFormularioAvaliacaoEndpointTests
{
    private const string Avaliacoes = "/api/configuracao/admin/avaliacoes-de-formulario";

    private static readonly string[] ChavesDoCampo = ["visivel", "obrigatorio", "impedido", "restricoesVioladas", "opcoes"];

    private readonly ConfiguracaoEndpointFixture _fixture;

    public ModeloFormularioAvaliacaoEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory(DisplayName = "A avaliação sem cadastro das regras do modelo dá o mesmo resultado da pré-visualização do modelo")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AvaliarSemCadastro_MesmoResultadoDaPreVisualizacao(bool quilombola)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CriarAsync(client, InscricaoComOpcoes(CodigoUnico()));
        JsonObject formulario = await RenderizavelAsync(client, id);
        Dictionary<string, object> respostas = new() { ["QUILOMBOLA"] = quilombola };

        JsonObject avaliacao = await AvaliarAsync(client, formulario["regras"], respostas);
        JsonObject preVisualizacao = await PreVisualizacaoAsync(client, id, respostas);

        ConferirCamposIguaisAosDaPreVisualizacao(avaliacao, preVisualizacao["itens"]!.AsArray());
        ConferirOpcoesDaFormaDeConclusao(avaliacao, quilombola);
        ConferirEtapasIguaisAsSecoes(avaliacao, preVisualizacao);
    }

    [Fact(DisplayName = "A avaliação sem cadastro recusa regras que não formam formulário com a causa")]
    public async Task AvaliarSemCadastro_RegrasMalformadas_Recusa()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage recusa = await EnviarAsync(client, HttpMethod.Post, Avaliacoes, new { regras = RegrasComAgregadoInvalido() });

        recusa.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await recusa.Content.ReadAsStringAsync()).Should().Contain("uniplus.formulario_portavel.operacao_de_agregado_invalida");
    }

    private static async Task<JsonObject> AvaliarAsync(HttpClient client, JsonNode? regras, Dictionary<string, object> respostas)
    {
        HttpResponseMessage resposta = await EnviarAsync(client, HttpMethod.Post, Avaliacoes, new { regras, respostas });
        JsonObject avaliacao = await LerAsync(resposta);
        resposta.Content.Headers.ContentType!.MediaType.Should().Be("application/vnd.uniplus.avaliacao-de-formulario.v1+json");
        return avaliacao;
    }

    private static void ConferirCamposIguaisAosDaPreVisualizacao(JsonObject avaliacao, JsonArray itens)
    {
        itens.Should().NotBeEmpty();
        foreach (JsonNode? item in itens)
        {
            ConferirCampo(Unico(avaliacao["campos"]!, "fatoCodigo", item!["fatoCodigo"]!.GetValue<string>()), item);
        }
    }

    private static void ConferirCampo(JsonNode campo, JsonNode item)
    {
        foreach (string chave in ChavesDoCampo)
        {
            (campo[chave]?.ToJsonString()).Should().Be(item[chave]?.ToJsonString(), $"'{chave}' do campo {item["fatoCodigo"]} é o mesmo nos dois caminhos");
        }
    }

    private static void ConferirOpcoesDaFormaDeConclusao(JsonObject avaliacao, bool quilombola) =>
        Unico(avaliacao["campos"]!, "fatoCodigo", "FORMA_CONCLUSAO_EM")["opcoes"]!["codigos"]!.AsArray()
            .Select(static c => c!.GetValue<string>()).Should().Equal(quilombola ? ["EJA", "REGULAR"] : ["REGULAR"], "as opções seguem a resposta anterior");

    // No modelo, o código da etapa nas regras é o da seção: as duas listas se comparam direto.
    private static void ConferirEtapasIguaisAsSecoes(JsonObject avaliacao, JsonObject preVisualizacao) =>
        Pares(avaliacao["etapas"]!).Should().Equal(Pares(preVisualizacao["secoes"]!));

    private static List<string> Pares(JsonNode lista) =>
        [.. lista.AsArray().Select(e => $"{e!["codigo"]!.GetValue<string>()}={e["visivel"]!.GetValue<string>()}")];

    private static object RegrasComAgregadoInvalido() => new
    {
        etapas = new[] { new { codigo = "DADOS", exibicao = (object?)null, itens = Array.Empty<object>(), grupos = Array.Empty<object>() } },
        termos = Array.Empty<object>(),
        derivacoes = Array.Empty<object>(),
        agregados = new[] { new { codigo = "ALGUEM", grupoCodigo = "FAMILIA", fatoDeMembro = "SEM_RENDA", operacao = "SOMA" } },
    };
}
