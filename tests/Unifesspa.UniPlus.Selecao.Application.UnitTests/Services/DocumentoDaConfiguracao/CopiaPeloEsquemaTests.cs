namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.DocumentoDaConfiguracao;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

public sealed class CopiaPeloEsquemaTests
{
    [Fact]
    public void Copiar_DecimalCanonicoEmCampoNumerico_ProduzNumero()
    {
        JsonObject copia = Copiar("""{"valor":"12.3400"}""", new() { ["valor"] = FormaDeTeste.Valor(false, "number") });
        copia["valor"]!.GetValue<decimal>().Should().Be(12.34m);
    }

    [Theory]
    [InlineData("\"123\"")]
    [InlineData("123")]
    public void Copiar_ValorEmCampoQueAceitaTextoENumero_PreservaTipo(string valor)
    {
        JsonObject copia = Copiar($$"""{"valor":{{valor}}}""", new() { ["valor"] = FormaDeTeste.Valor(false, "string", "number", "boolean", "null") });
        copia.ToJsonString().Should().Be($$"""{"valor":{{valor}}}""");
    }

    [Fact]
    public void Copiar_DecimalCanonicoEmCampoDeTexto_PreservaTexto()
    {
        JsonObject copia = Copiar("""{"valor":"123"}""", new() { ["valor"] = FormaDeTeste.Valor(false, "string") });
        copia.ToJsonString().Should().Be("""{"valor":"123"}""");
    }

    [Fact]
    public void Copiar_TextoInvalidoEmCampoString_PreservaTexto()
    {
        JsonObject copia = Copiar("""{"valor":"12,34"}""", new() { ["valor"] = FormaDeTeste.Valor(false, "string") });
        copia["valor"]!.GetValue<string>().Should().Be("12,34");
    }

    [Fact]
    public void Copiar_Nulos_ConservaApenasAceitos()
    {
        JsonObject copia = Copiar("""{"aceito":null,"recusado":null}""", new()
        {
            ["aceito"] = FormaDeTeste.Valor(false, "string", "null"),
            ["recusado"] = FormaDeTeste.Valor(false, "string"),
        });
        copia.ContainsKey("aceito").Should().BeTrue();
        copia["aceito"].Should().BeNull();
        copia.ContainsKey("recusado").Should().BeFalse();
    }

    [Fact]
    public void Copiar_CampoNaoDeclarado_DescartaCampo()
    {
        JsonObject copia = Copiar("""{"declarado":"sim","extra":"nao"}""", new() { ["declarado"] = FormaDeTeste.Valor() });
        copia.ToJsonString().Should().Be("""{"declarado":"sim"}""");
    }

    [Fact]
    public void Copiar_CampoSomenteLeituraNoContexto_CopiaPeloNomeExato()
    {
        JsonObject copia = Copiar("""{"contexto":{"tipoRotulo":"Nome","outroRotulo":"Ignorado"}}""", new()
        {
            ["tipoRotulo"] = FormaDeTeste.Valor(true),
            ["outroRotulo"] = FormaDeTeste.Valor(),
        });
        copia.ToJsonString().Should().Be("""{"tipoRotulo":"Nome"}""");
    }

    [Fact]
    public void Copiar_CampoSomenteLeituraComNomeSemSufixoNoContexto_NaoCopia()
    {
        JsonObject copia = Copiar("""{"contexto":{"tipo":"Nome"}}""", new() { ["tipoRotulo"] = FormaDeTeste.Valor(true) });
        copia.ToJsonString().Should().Be("{}");
    }

    [Fact]
    public void Copiar_ListaDeRotulosNoContexto_CopiaNaMesmaOrdem()
    {
        JsonObject copia = Copiar("""{"contexto":{"tiposDeDecisaoRotulos":["Segundo","Primeiro"]}}""", new()
        {
            ["tiposDeDecisaoRotulos"] = FormaDeTeste.Lista(FormaDeTeste.Valor(false, "string"), somenteLeitura: true),
        });
        copia.ToJsonString().Should().Be("""{"tiposDeDecisaoRotulos":["Segundo","Primeiro"]}""");
    }

    [Fact]
    public void Copiar_RotuloNoProprioObjetoEnoContexto_PrefereOProprioObjeto()
    {
        JsonObject copia = Copiar("""{"tipoRotulo":"Direto","contexto":{"tipoRotulo":"Contexto"}}""", new() { ["tipoRotulo"] = FormaDeTeste.Valor(true) });
        copia.ToJsonString().Should().Be("""{"tipoRotulo":"Direto"}""");
    }

    [Fact]
    public void Copiar_CampoQueNaoESomenteLeituraNoContexto_NaoCopia()
    {
        JsonObject copia = Copiar("""{"contexto":{"tipoRotulo":"Nome"}}""", new() { ["tipoRotulo"] = FormaDeTeste.Valor() });
        copia.ToJsonString().Should().Be("{}");
    }

    [Fact]
    public void Copiar_ListaDeObjetos_CopiaSomenteCamposDeclarados()
    {
        FormaDoNo itens = FormaDeTeste.Objeto(new() { ["nome"] = FormaDeTeste.Valor() });
        JsonObject copia = Copiar("""{"itens":[{"nome":"A","extra":1},{"nome":"B"}]}""", new() { ["itens"] = FormaDeTeste.Lista(itens) });
        copia.ToJsonString().Should().Be("""{"itens":[{"nome":"A"},{"nome":"B"}]}""");
    }

    [Fact]
    public void Copiar_ObjetoAninhado_AplicaFormaDoFilho()
    {
        FormaDoNo filho = FormaDeTeste.Objeto(new() { ["valor"] = FormaDeTeste.Valor(false, "integer") });
        JsonObject copia = Copiar("""{"filho":{"valor":"3","extra":true}}""", new() { ["filho"] = filho });
        copia.ToJsonString().Should().Be("""{"filho":{"valor":3}}""");
    }

    private static JsonObject Copiar(string origem, Dictionary<string, FormaDoNo> propriedades) =>
        CopiaPeloEsquema.Copiar(JsonNode.Parse(origem)!.AsObject(), FormaDeTeste.Objeto(propriedades));
}
