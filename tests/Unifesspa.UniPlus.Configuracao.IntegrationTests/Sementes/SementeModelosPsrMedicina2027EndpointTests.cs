namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Sementes;

using System.Net;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.ModelosFormulario.ModeloFormularioHttp;

/// <summary>
/// Os modelos de inscrição e de habilitação do edital de Medicina 2027 existem ao subir o sistema,
/// gravados pelas sementes do módulo, e a API os aceita como se tivessem sido cadastrados pela tela.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class SementeModelosPsrMedicina2027EndpointTests
{
    private readonly ConfiguracaoEndpointFixture _fixture;

    public SementeModelosPsrMedicina2027EndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory(DisplayName = "O modelo semeado está ativo, tem campos, e o conteúdo dele volta à escrita aceito pela conferência do catálogo")]
    [InlineData(SementePsrMedicina2027.ModeloDeInscricaoSemProva)]
    [InlineData(SementePsrMedicina2027.ModeloDeInscricaoComProva)]
    [InlineData(SementePsrMedicina2027.ModeloDeHabilitacao)]
    public async Task ModeloSemeado_EhAceitoPelaEscrita(string codigo)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        JsonObject modelo = await ObterPeloCodigoAsync(client, codigo);

        modelo["ativo"]!.GetValue<bool>().Should().BeTrue();
        modelo["tipoProcessoCodigo"]!.GetValue<string>().Should().Be(SementePsrMedicina2027.TipoProcessoCodigo);
        ConferirQueTemCampos(modelo["conteudo"]!);

        // A escrita confere o conteúdo contra o catálogo de fatos, os termos e o tipo de processo:
        // aceito aqui, o modelo é o que a tela teria gravado.
        HttpResponseMessage reescrita = await ReescreverAsync(client, modelo);
        reescrita.StatusCode.Should().Be(HttpStatusCode.NoContent, await reescrita.Content.ReadAsStringAsync());
    }

    [Theory(DisplayName = "A pré-visualização do modelo semeado responde")]
    [InlineData(SementePsrMedicina2027.ModeloDeInscricaoSemProva)]
    [InlineData(SementePsrMedicina2027.ModeloDeInscricaoComProva)]
    [InlineData(SementePsrMedicina2027.ModeloDeHabilitacao)]
    public async Task ModeloSemeado_PreVisualiza(string codigo)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        JsonObject modelo = await ObterPeloCodigoAsync(client, codigo);

        HttpResponseMessage resposta = await EnviarAsync(
            client, HttpMethod.Post, $"{Base}/{modelo["id"]!.GetValue<Guid>()}/pre-visualizacao",
            new { respostas = new Dictionary<string, object>() });

        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
    }

    private static async Task<JsonObject> ObterPeloCodigoAsync(HttpClient client, string codigo)
    {
        HttpResponseMessage lista = await EnviarAsync(client, HttpMethod.Get, $"{Base}?tipoProcesso={SementePsrMedicina2027.TipoProcessoCodigo}");
        lista.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonNode listados = JsonNode.Parse(await lista.Content.ReadAsStringAsync())!;
        return await ObterAsync(client, Unico(listados, "codigo", codigo)["id"]!.GetValue<Guid>());
    }

    private static void ConferirQueTemCampos(JsonNode conteudo)
    {
        IEnumerable<JsonNode> campos = conteudo["itens"]!.AsArray()
            .Concat(conteudo["grupos"]!.AsArray().SelectMany(static g => g!["subitens"]!.AsArray()))!;
        campos.Should().NotBeEmpty();
    }

    private static async Task<HttpResponseMessage> ReescreverAsync(HttpClient client, JsonObject modelo) =>
        await EnviarAsync(client, HttpMethod.Put, $"{Base}/{modelo["id"]!.GetValue<Guid>()}", new
        {
            nome = modelo["nome"]!.GetValue<string>(),
            descricao = modelo["descricao"]!.GetValue<string>(),
            tipoProcessoCodigo = SementePsrMedicina2027.TipoProcessoCodigo,
            conteudo = modelo["conteudo"],
        });
}
