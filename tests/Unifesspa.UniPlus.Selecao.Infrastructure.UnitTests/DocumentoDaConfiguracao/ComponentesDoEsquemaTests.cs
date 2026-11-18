namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.DocumentoDaConfiguracao;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Infrastructure.DocumentoDaConfiguracao;

/// <summary>Os componentes da especificação saem do esquema pelas referências alcançadas, com os nomes e as referências reescritas.</summary>
public sealed class ComponentesDoEsquemaTests
{
    private const string Documento = """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "$id": "https://exemplo.test/documento.schema.json",
          "type": "object",
          "properties": {
            "inteira": { "$ref": "distribuicao-de-vagas.schema.json", "description": "irmã do ref", "readOnly": true },
            "definicaoDeOutroArquivo": { "$ref": "comuns.schema.json#/$defs/chave" },
            "definicaoLocal": { "$ref": "#/$defs/local" },
            "filhos": { "type": "array", "items": { "$ref": "#" } }
          },
          "$defs": { "local": { "type": "string" }, "naoCitada": { "type": "integer" } }
        }
        """;

    private const string DistribuicaoDeVagas = """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "$id": "https://exemplo.test/distribuicao-de-vagas.schema.json",
          "type": "object",
          "properties": { "chave": { "$ref": "comuns.schema.json#/$defs/chave" } }
        }
        """;

    private const string Comuns = """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "$id": "https://exemplo.test/comuns.schema.json",
          "$defs": { "chave": { "type": "string" }, "naoCitada": { "type": "integer" } }
        }
        """;

    private static Dictionary<string, JsonNode> Arquivos() => new(StringComparer.Ordinal)
    {
        ["documento.schema.json"] = JsonNode.Parse(Documento)!,
        ["distribuicao-de-vagas.schema.json"] = JsonNode.Parse(DistribuicaoDeVagas)!,
        ["comuns.schema.json"] = JsonNode.Parse(Comuns)!,
    };

    private static SortedDictionary<string, JsonNode> Gerar(Dictionary<string, JsonNode> arquivos) =>
        new ComponentesDoEsquema(arquivos).Gerar();

    [Fact(DisplayName = "Vira componente só o que as referências alcançam, com o nome do arquivo e da definição")]
    public void Gerar_NomeiaApenasOAlcancado()
    {
        SortedDictionary<string, JsonNode> componentes = Gerar(Arquivos());

        componentes.Keys.Should().Equal(
            "DocumentoDaConfiguracao",
            "DocumentoDaConfiguracaoComunsChave",
            "DocumentoDaConfiguracaoDistribuicaoDeVagas",
            "DocumentoDaConfiguracaoDocumentoLocal");
    }

    [Theory(DisplayName = "A referência, em qualquer das quatro formas, aponta para o componente")]
    [InlineData("inteira", "#/components/schemas/DocumentoDaConfiguracaoDistribuicaoDeVagas")]
    [InlineData("definicaoDeOutroArquivo", "#/components/schemas/DocumentoDaConfiguracaoComunsChave")]
    [InlineData("definicaoLocal", "#/components/schemas/DocumentoDaConfiguracaoDocumentoLocal")]
    [InlineData("filhos/items", "#/components/schemas/DocumentoDaConfiguracao")]
    public void Gerar_ReescreveAReferencia(string propriedade, string esperada)
    {
        ArgumentNullException.ThrowIfNull(propriedade);
        JsonNode raiz = Gerar(Arquivos())["DocumentoDaConfiguracao"]["properties"]!;

        JsonNode propriedadeLida = propriedade.Split('/').Aggregate(raiz, static (no, passo) => no[passo]!);

        propriedadeLida["$ref"]!.GetValue<string>().Should().Be(esperada);
    }

    [Fact(DisplayName = "A referência mantém as palavras-chave que a acompanham")]
    public void Gerar_MantemAsPalavrasIrmasDaReferencia()
    {
        JsonNode inteira = Gerar(Arquivos())["DocumentoDaConfiguracao"]["properties"]!["inteira"]!;

        inteira["description"]!.GetValue<string>().Should().Be("irmã do ref");
        inteira["readOnly"]!.GetValue<bool>().Should().BeTrue();
    }

    [Fact(DisplayName = "O componente não leva $id, $schema nem $defs")]
    public void Gerar_RetiraAsPalavrasDoArquivoDeOrigem()
    {
        foreach (JsonNode componente in Gerar(Arquivos()).Values)
        {
            componente.AsObject().Select(static propriedade => propriedade.Key).Should().NotContain(["$id", "$schema", "$defs"]);
        }
    }

    [Fact(DisplayName = "A raiz de um arquivo que só guarda definições e a definição não citada não viram componente")]
    public void Gerar_NaoPublicaOQueNaoECitado()
    {
        SortedDictionary<string, JsonNode> componentes = Gerar(Arquivos());

        componentes.Keys.Should().NotContain(["DocumentoDaConfiguracaoComuns", "DocumentoDaConfiguracaoComunsNaoCitada", "DocumentoDaConfiguracaoDocumentoNaoCitada"]);
    }

    [Fact(DisplayName = "Dois nomes iguais para partes diferentes do esquema derrubam a geração")]
    public void Gerar_ComColisaoDeNome_Falha()
    {
        Dictionary<string, JsonNode> arquivos = Arquivos();
        arquivos["distribuicao.schema.json"] = JsonNode.Parse("""{ "$defs": { "deVagas": { "type": "string" } } }""")!;
        arquivos["documento.schema.json"]["properties"]!["colide"] = JsonNode.Parse("""{ "$ref": "distribuicao.schema.json#/$defs/deVagas" }""");

        Action gerar = () => Gerar(arquivos);

        gerar.Should().Throw<InvalidOperationException>().WithMessage("*DocumentoDaConfiguracaoDistribuicaoDeVagas*");
    }

    [Theory(DisplayName = "A referência que não é para a raiz nem para uma definição, ou que não existe, derruba a geração")]
    [InlineData("#/properties/filhos")]
    [InlineData("#/$defs/inexistente")]
    [InlineData("inexistente.schema.json")]
    public void Gerar_ComReferenciaForaDasFormas_Falha(string referencia)
    {
        ArgumentNullException.ThrowIfNull(referencia);
        Dictionary<string, JsonNode> arquivos = Arquivos();
        arquivos["documento.schema.json"]["properties"]!["invalida"] = new JsonObject { ["$ref"] = referencia };

        Action gerar = () => Gerar(arquivos);

        gerar.Should().Throw<InvalidOperationException>();
    }

    [Fact(DisplayName = "A geração não altera o esquema de origem e devolve nós novos a cada vez")]
    public void Gerar_NaoAlteraAOrigemENaoCompartilhaNos()
    {
        Dictionary<string, JsonNode> arquivos = Arquivos();
        Dictionary<string, JsonNode> copia = arquivos.ToDictionary(static par => par.Key, static par => par.Value.DeepClone(), StringComparer.Ordinal);

        SortedDictionary<string, JsonNode> primeira = Gerar(arquivos);
        SortedDictionary<string, JsonNode> segunda = Gerar(arquivos);

        arquivos.Should().OnlyContain(par => JsonNode.DeepEquals(par.Value, copia[par.Key]));
        primeira.Keys.Should().Equal(segunda.Keys);
        primeira.Should().OnlyContain(par => JsonNode.DeepEquals(par.Value, segunda[par.Key]) && !ReferenceEquals(par.Value, segunda[par.Key]));
    }
}
