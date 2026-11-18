namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Contratos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

/// <summary>
/// O comparador de compatibilidade do esquema, exercitado com pares sintéticos de versão publicada e atual, e a conferência
/// do esquema atual contra as cópias publicadas que o repositório congela na primeira tag que as divulga.
/// </summary>
public sealed class CompatibilidadeDoEsquemaTests
{
    private const string Base = """
        { "type": "object", "additionalProperties": false, "required": ["a"],
          "properties": { "a": { "type": "string", "maxLength": 10, "pattern": "^[a-z]+$", "enum": ["x", "y"] }, "b": { "type": "integer", "minimum": 1 } } }
        """;

    public static TheoryData<string, string, string> MudancasIncompativeis => new()
    {
        { "propriedade retirada", """{ "properties": { "b": null } }""", "/properties/b" },
        { "obrigatoriedade nova", """{ "required": ["a", "b"] }""", "/required" },
        { "tipo trocado", """{ "properties": { "b": { "type": "string" } } }""", "/properties/b/type" },
        { "enum estreitado", """{ "properties": { "a": { "enum": ["x"] } } }""", "/properties/a/enum" },
        { "limite máximo apertado", """{ "properties": { "a": { "maxLength": 5 } } }""", "/properties/a/maxLength" },
        { "limite mínimo apertado", """{ "properties": { "b": { "minimum": 2 } } }""", "/properties/b/minimum" },
        { "padrão novo", """{ "properties": { "a": { "pattern": "^x$" } } }""", "/properties/a/pattern" },
        { "constante nova", """{ "properties": { "a": { "const": "x" } } }""", "/properties/a/const" },
        { "mínimo exclusivo novo", """{ "properties": { "b": { "exclusiveMinimum": 3 } } }""", "/properties/b/exclusiveMinimum" },
        { "alternativas novas", """{ "properties": { "b": { "anyOf": [ { "type": "integer" } ] } } }""", "/properties/b/anyOf" },
    };

    public static TheoryData<string, string> MudancasCompativeis => new()
    {
        { "propriedade opcional nova", """{ "properties": { "c": { "type": "boolean" } } }""" },
        { "valor novo de enum", """{ "properties": { "a": { "enum": ["x", "y", "z"] } } }""" },
        { "limite máximo relaxado", """{ "properties": { "a": { "maxLength": 20 } } }""" },
        { "limite mínimo relaxado", """{ "properties": { "b": { "minimum": 0 } } }""" },
        { "padrão retirado", """{ "properties": { "a": { "pattern": null } } }""" },
        { "descrição nova", """{ "description": "texto" }""" },
    };

    [Theory(DisplayName = "A mudança que invalida documento da versão publicada é recusada com o caminho")]
    [MemberData(nameof(MudancasIncompativeis))]
    public void MudancaIncompativel_Recusa(string descricao, string alteracao, string caminho)
    {
        _ = descricao;

        CompatibilidadeDoEsquema.Violacoes(JsonNode.Parse(Base)!, Aplicar(alteracao)).Should().ContainMatch($"#{caminho}*");
    }

    [Theory(DisplayName = "A mudança que mantém válido o documento da versão publicada é aceita")]
    [MemberData(nameof(MudancasCompativeis))]
    public void MudancaCompativel_Aceita(string descricao, string alteracao)
    {
        _ = descricao;

        CompatibilidadeDoEsquema.Violacoes(JsonNode.Parse(Base)!, Aplicar(alteracao)).Should().BeEmpty();
    }

    [Fact(DisplayName = "A propriedade nova num objeto que aceitava propriedade adicional é recusada")]
    public void PropriedadeNovaEmObjetoAberto_Recusa()
    {
        JsonNode aberto = JsonNode.Parse("""{ "type": "object", "properties": { "a": { "type": "string" } } }""")!;
        JsonNode comNova = JsonNode.Parse("""{ "type": "object", "properties": { "a": { "type": "string" }, "x": { "type": "string" } } }""")!;

        CompatibilidadeDoEsquema.Violacoes(aberto, comNova).Should().ContainMatch("#/properties/x*");
    }

    [Fact(DisplayName = "O esquema atual é compatível com cada cópia publicada")]
    public void EsquemaAtual_ContraCopiasPublicadas_SemViolacao()
    {
        string pasta = Path.Join(ContratoDaConfiguracao.PastaDoContrato, "publicadas", "v1");
        string[] copias = Directory.Exists(pasta) ? Directory.GetFiles(pasta, "*.schema.json") : [];

        string[] violacoes = [.. copias.SelectMany(copia => Violacoes(copia))];

        violacoes.Should().BeEmpty();
    }

    private static string[] Violacoes(string copia)
    {
        string arquivo = Path.GetFileName(copia);
        return ContratoDaConfiguracao.ArquivosDoEsquema.Contains(arquivo, StringComparer.Ordinal)
            ? [.. CompatibilidadeDoEsquema.Violacoes(JsonNode.Parse(File.ReadAllText(copia))!, ContratoDaConfiguracao.Esquema(arquivo)).Select(v => $"{arquivo}{v}")]
            : [$"{arquivo}: arquivo publicado retirado"];
    }

    // Aplica a alteração sobre a base: as propriedades se fundem (nulo retira a propriedade); as demais chaves substituem.
    private static JsonObject Aplicar(string alteracao)
    {
        JsonObject resultado = JsonNode.Parse(Base)!.AsObject();
        foreach ((string chave, JsonNode? valor) in JsonNode.Parse(alteracao)!.AsObject().ToList())
        {
            if (chave == "properties")
            {
                Fundir(resultado["properties"]!.AsObject(), valor!.AsObject());
                continue;
            }

            resultado[chave] = valor!.DeepClone();
        }

        return resultado;
    }

    private static void Fundir(JsonObject destino, JsonObject origem)
    {
        foreach ((string nome, JsonNode? valor) in origem.ToList())
        {
            FundirPropriedade(destino, nome, valor);
        }
    }

    private static void AtribuirOuRetirar(JsonObject destino, string nome, JsonNode? valor)
    {
        if (valor is null)
        {
            destino.Remove(nome);
            return;
        }

        destino[nome] = valor.DeepClone();
    }

    private static void FundirPropriedade(JsonObject destino, string nome, JsonNode? valor)
    {
        if (valor is null)
        {
            destino.Remove(nome);
        }
        else if (destino[nome] is JsonObject existente && valor is JsonObject nova)
        {
            nova.ToList().ForEach(par => AtribuirOuRetirar(existente, par.Key, par.Value));
        }
        else
        {
            destino[nome] = valor.DeepClone();
        }
    }
}
