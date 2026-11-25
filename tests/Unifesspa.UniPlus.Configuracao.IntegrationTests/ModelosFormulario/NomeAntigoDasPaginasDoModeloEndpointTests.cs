namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.ModelosFormulario;

using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.ModelosFormulario.ModeloFormularioHttp;
using static Unifesspa.UniPlus.Configuracao.IntegrationTests.ModelosFormulario.ModelosParaTeste;

[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class NomeAntigoDasPaginasDoModeloEndpointTests(ConfiguracaoEndpointFixture fixture)
{
    [Fact]
    public async Task Criar_ComEtapasNoConteudo_RecusaBlocoExigidoEmPaginas()
    {
        using HttpClient cliente = fixture.Factory.CreateClient();
        JsonObject pedido = JsonSerializer.SerializeToNode(Habilitacao(CodigoUnico()))!.AsObject();
        JsonObject conteudo = pedido["conteudo"]!.AsObject();
        conteudo["etapas"] = conteudo["paginas"]!.DeepClone();
        conteudo.Remove("paginas");
        using HttpResponseMessage resposta = await EnviarAsync(cliente, HttpMethod.Post, Base, pedido);
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        problema.RootElement.GetProperty("errors").EnumerateArray()
            .Select(static erro => (erro.GetProperty("field").GetString(), erro.GetProperty("code").GetString()))
            .Should().Contain(("conteudo.paginas", "uniplus.estrutura_formulario.bloco_exigido_ausente"));
    }
}
