namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.ModelosFormulario;

using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.ModelosFormulario.ModeloFormularioHttp;
using static Unifesspa.UniPlus.Configuracao.IntegrationTests.ModelosFormulario.ModelosParaTeste;

[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class FormatoDoGrupoNoModeloEndpointTests(ConfiguracaoEndpointFixture fixture)
{
    [Fact]
    public async Task Criar_GrupoForaDoFormato_RecusaNoCampo()
    {
        const string codigoInvalido = "serie ruim";
        using HttpClient cliente = fixture.Factory.CreateClient();
        JsonNode modelo = JsonSerializer.SerializeToNode(Habilitacao(CodigoUnico()))!;
        modelo["conteudo"]!["grupos"] = new JsonArray(GrupoComCodigoInvalido(codigoInvalido));
        HttpResponseMessage resposta = await EnviarAsync(cliente, HttpMethod.Post, Base, modelo);
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        JsonElement erro = problema.RootElement.GetProperty("errors").EnumerateArray().Single(e => e.GetProperty("field").GetString() == "conteudo.grupos[0].codigo");
        erro.GetProperty("code").GetString().Should().Be("uniplus.grupo_formulario.codigo_invalido");
        erro.GetProperty("message").GetString().Should().NotContain(codigoInvalido);
    }

    private static JsonNode GrupoComCodigoInvalido(string codigoInvalido)
    {
        GrupoColetadoInput grupo = EntradaDoGrupoColetadoDeTeste.Compondo("FAMILIA", 2).ComRotulo("Família").NaSecao("DADOS")
            .ComCampos(EntradaDoFatoColetadoDeTeste.Coletando("PARENTESCO").DoTipo("ESCOLHA_UNICA").Construir()).Construir();
        JsonNode grupoInvalido = JsonSerializer.SerializeToNode(grupo, JsonSerializerOptions.Web)!;
        grupoInvalido["codigo"] = codigoInvalido;
        return grupoInvalido;
    }
}
