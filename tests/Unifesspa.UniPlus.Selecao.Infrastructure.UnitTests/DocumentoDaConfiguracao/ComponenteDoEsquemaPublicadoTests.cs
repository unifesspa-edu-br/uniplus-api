namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.DocumentoDaConfiguracao;

using System.Globalization;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Microsoft.OpenApi;

using Unifesspa.UniPlus.Selecao.Infrastructure.DocumentoDaConfiguracao;

/// <summary>O componente é escrito exatamente como o esquema o declara, inclusive onde o modelo de schema da biblioteca mudaria o significado.</summary>
public sealed class ComponenteDoEsquemaPublicadoTests
{
    private static string Escrever(IOpenApiSchema schema)
    {
        using StringWriter texto = new();
        schema.SerializeAsV31(new OpenApiJsonWriter(texto));
        return texto.ToString();
    }

    private static void DeveSerEscritoComoEsta(string json)
    {
        JsonNode declarado = JsonNode.Parse(json)!;

        string escrito = Escrever(new ComponenteDoEsquemaPublicado(declarado));

        JsonNode.DeepEquals(JsonNode.Parse(escrito), declarado).Should().BeTrue(escrito);
    }

    [Fact(DisplayName = "A const numérica da raiz continua número")]
    public void Escrever_ConstNumericaContinuaNumero() =>
        DeveSerEscritoComoEsta("""{ "type": "object", "properties": { "versaoFormato": { "const": 1 } } }""");

    [Fact(DisplayName = "A const booleana de uma condição continua booleana")]
    public void Escrever_ConstBooleanaContinuaBooleana() =>
        DeveSerEscritoComoEsta("""{ "if": { "properties": { "aplica": { "const": true }, "temProvaPresencial": { "const": false } } } }""");

    [Fact(DisplayName = "O esquema false de uma propriedade do else não some")]
    public void Escrever_EsquemaFalsoNaPropriedadeNaoSome() =>
        DeveSerEscritoComoEsta("""{ "else": { "properties": { "regra": false, "fator": false } } }""");

    [Fact(DisplayName = "O type em lista mantém a ordem declarada")]
    public void Escrever_TypeEmListaMantemAOrdemDeclarada()
    {
        const string declarado = """{ "type": ["string", "boolean", "number", "null"] }""";

        string escrito = Escrever(new ComponenteDoEsquemaPublicado(JsonNode.Parse(declarado)!));

        JsonNode.Parse(escrito)!["type"]!.ToJsonString().Should().Be("""["string","boolean","number","null"]""");
    }

    [Fact(DisplayName = "As properties vazias não somem")]
    public void Escrever_PropertiesVaziasNaoSomem() =>
        DeveSerEscritoComoEsta("""{ "type": "object", "properties": {}, "additionalProperties": false }""");

    [Fact(DisplayName = "A raiz que é só uma referência com título e descrição é escrita como está")]
    public void Escrever_RaizComReferenciaEscritaComoEsta() =>
        DeveSerEscritoComoEsta("""{ "title": "Condição do desempate", "description": "Fato e valor.", "$ref": "#/components/schemas/Alvo" }""");

    [Fact(DisplayName = "Os números decimais são escritos com ponto, qualquer que seja a cultura da thread")]
    public void Escrever_NumerosDecimais_IndependemDaCulturaDaThread()
    {
        CultureInfo anterior = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("pt-BR");
        try
        {
            DeveSerEscritoComoEsta("""{ "multipleOf": 0.0001, "maximum": 99.9999 }""");
            CultureInfo.CurrentCulture.Name.Should().Be("pt-BR");
        }
        finally
        {
            CultureInfo.CurrentCulture = anterior;
        }
    }

    [Fact(DisplayName = "Duas escritas dão o mesmo texto e o nó declarado não é alterado")]
    public void Escrever_DuasVezes_DaOMesmoTexto()
    {
        JsonNode declarado = JsonNode.Parse("""{ "type": ["null", "string"], "const": 1 }""")!;
        string antes = declarado.ToJsonString();
        ComponenteDoEsquemaPublicado componente = new(declarado);

        string primeira = Escrever(componente);
        string segunda = Escrever(componente);

        segunda.Should().Be(primeira);
        declarado.ToJsonString().Should().Be(antes);
    }

    [Fact(DisplayName = "A cópia rasa é outro componente sobre o mesmo conteúdo")]
    public void CreateShallowCopy_EscreveOMesmoConteudo()
    {
        ComponenteDoEsquemaPublicado original = new(JsonNode.Parse("""{ "const": 1 }""")!);

        IOpenApiSchema copia = original.CreateShallowCopy();

        copia.Should().BeOfType<ComponenteDoEsquemaPublicado>().Which.Should().NotBeSameAs(original);
        Escrever(copia).Should().Be(Escrever(original));
    }

    [Fact(DisplayName = "As versões 3.0 e 2.0 da OpenAPI não se escrevem")]
    public void EscreverEmOutrasVersoes_Lanca()
    {
        ComponenteDoEsquemaPublicado componente = new(JsonNode.Parse("{}")!);
        using StringWriter texto = new();
        OpenApiJsonWriter escritor = new(texto);

        Action v3 = () => componente.SerializeAsV3(escritor);
        Action v2 = () => componente.SerializeAsV2(escritor);

        v3.Should().Throw<NotSupportedException>().WithMessage("*3.1*");
        v2.Should().Throw<NotSupportedException>().WithMessage("*3.1*");
    }

    [Fact(DisplayName = "O componente não aceita alteração da descrição")]
    public void AlterarDescricao_Lanca()
    {
        ComponenteDoEsquemaPublicado componente = new(JsonNode.Parse("{}")!);

        Action alterar = () => componente.Description = "outra";

        alterar.Should().Throw<NotSupportedException>();
    }

    [Fact(DisplayName = "Cada componente do esquema real é escrito exatamente como foi gerado")]
    public void Escrever_ComponentesDoEsquemaReal_SaoOsGerados()
    {
        foreach ((string nome, JsonNode gerado) in new ComponentesDoEsquema(ArquivosDoEsquema.DoAssembly().PorNome).Gerar())
        {
            string escrito = Escrever(new ComponenteDoEsquemaPublicado(gerado));

            JsonNode.DeepEquals(JsonNode.Parse(escrito), gerado).Should().BeTrue(nome);
        }
    }
}
