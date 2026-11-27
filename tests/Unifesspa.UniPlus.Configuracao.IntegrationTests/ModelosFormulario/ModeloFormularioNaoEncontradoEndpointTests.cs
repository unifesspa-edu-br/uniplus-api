namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.ModelosFormulario;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.ModelosFormulario.ModeloFormularioHttp;

/// <summary>
/// A consulta de modelo de formulário por Id inexistente responde 404 com o código de modelo não encontrado,
/// em cada rota que recebe o Id do modelo.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class ModeloFormularioNaoEncontradoEndpointTests
{
    private const string Codigo = "uniplus.configuracao.modelo_formulario.nao_encontrado";

    private readonly ConfiguracaoEndpointFixture _fixture;

    public ModeloFormularioNaoEncontradoEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "GET admin/modelos-formulario/{id} de modelo inexistente responde 404 com o código do modelo")]
    public async Task Obter_Inexistente_Retorna404ComCodigo()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await EnviarAsync(client, HttpMethod.Get, $"{Base}/{Guid.NewGuid()}");

        await RespostaDeNaoEncontrado.ConferirAsync(resposta, Codigo);
    }

    [Fact(DisplayName = "GET admin/modelos-formulario/{id}/apresentacao de modelo inexistente responde 404 com o código do modelo")]
    public async Task Apresentacao_Inexistente_Retorna404ComCodigo()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await EnviarAsync(client, HttpMethod.Get, $"{Base}/{Guid.NewGuid()}/apresentacao");

        await RespostaDeNaoEncontrado.ConferirAsync(resposta, Codigo);
    }

    [Fact(DisplayName = "POST admin/modelos-formulario/{id}/pre-visualizacao de modelo inexistente responde 404 com o código do modelo")]
    public async Task PreVisualizar_Inexistente_Retorna404ComCodigo()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        Dictionary<string, object> respostas = new() { ["QUILOMBOLA"] = false };

        using HttpResponseMessage resposta = await EnviarAsync(client, HttpMethod.Post, $"{Base}/{Guid.NewGuid()}/pre-visualizacao", new { respostas });

        await RespostaDeNaoEncontrado.ConferirAsync(resposta, Codigo);
    }
}
