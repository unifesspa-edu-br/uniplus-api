namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.OpenApi.Apoio;

using System.Text.Json.Nodes;

using Microsoft.OpenApi;

/// <summary>O documento OpenAPI como o contrato o publica, em JSON, com o que os testes precisam para ler os componentes e resolver referências.</summary>
internal sealed class DocumentoPublicado
{
    private const string PrefixoDaReferencia = "#/components/schemas/";

    private readonly JsonNode _raiz;

    public DocumentoPublicado(OpenApiDocument documento)
    {
        ArgumentNullException.ThrowIfNull(documento);

        using StringWriter texto = new();
        documento.SerializeAsV31(new OpenApiJsonWriter(texto));
        _raiz = JsonNode.Parse(texto.ToString()) ?? throw new InvalidOperationException("O documento publicado está vazio.");
    }

    /// <summary>O nome de cada componente de schema.</summary>
    public IReadOnlyList<string> Componentes => [.. Schemas.Select(static c => c.Key)];

    public JsonNode Componente(string nome) =>
        Schemas[nome] ?? throw new InvalidOperationException($"O documento publicado não tem o componente {nome}.");

    public JsonNode Propriedade(string componente, string nome) =>
        Componente(componente)["properties"]?[nome]
        ?? throw new InvalidOperationException($"O componente {componente} não tem a propriedade {nome}.");

    public JsonNode Parametro(string caminho, string metodo, string nome) =>
        _raiz["paths"]?[caminho]?[metodo]?["parameters"]?.AsArray().FirstOrDefault(p => p?["name"]?.GetValue<string>() == nome)
        ?? throw new InvalidOperationException($"A operação {metodo} {caminho} não tem o parâmetro {nome}.");

    /// <summary>O destino da referência <c>$ref</c> do nó, ou nada se o nó não é uma referência.</summary>
    public static string? Referencia(JsonNode no) => no["$ref"]?.GetValue<string>();

    /// <summary>O componente que o nó referencia; o próprio nó, se ele não é uma referência.</summary>
    public JsonNode Resolver(JsonNode no) =>
        Referencia(no) is { } destino && destino.StartsWith(PrefixoDaReferencia, StringComparison.Ordinal)
            ? Componente(destino[PrefixoDaReferencia.Length..])
            : no;

    /// <summary>O caminho de cada schema do documento que traz a lista <c>enum</c> dentro dele.</summary>
    public IReadOnlyList<string> SchemasComEnum() => [.. SchemasComEnum(_raiz, string.Empty)];

    private JsonObject Schemas =>
        _raiz["components"]?["schemas"]?.AsObject() ?? throw new InvalidOperationException("O documento publicado não tem componentes.");

    private static IEnumerable<string> SchemasComEnum(JsonNode? no, string caminho) => no switch
    {
        JsonObject objeto => objeto.ContainsKey("enum")
            ? [caminho]
            : objeto.SelectMany(p => SchemasComEnum(p.Value, $"{caminho}/{p.Key}")),
        JsonArray lista => lista.SelectMany((item, posicao) => SchemasComEnum(item, $"{caminho}[{posicao}]")),
        _ => [],
    };
}
