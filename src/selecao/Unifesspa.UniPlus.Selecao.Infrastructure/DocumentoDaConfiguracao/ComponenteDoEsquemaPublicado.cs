namespace Unifesspa.UniPlus.Selecao.Infrastructure.DocumentoDaConfiguracao;

using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.OpenApi;

/// <summary>
/// Um componente da especificação OpenAPI que carrega, sem alteração, o trecho do esquema que o <see cref="ComponentesDoEsquema"/>
/// gerou. É escrito como está: o modelo de schema da biblioteca não representa o esquema do documento fielmente (a
/// <c>const</c> que não é texto vira texto e o esquema booleano <c>false</c> some), e o leitor dela as aplica ao ler.
/// </summary>
/// <remarks>
/// As propriedades do modelo respondem ausente ou o padrão do esquema: o conteúdo só existe como JSON, e quem consulta o
/// modelo (os transformers da plataforma) não encontra nele o que não lhe cabe tratar. O nó nunca é alterado: a cópia rasa
/// aponta para o mesmo nó. Só a versão 3.1 da especificação é escrita, porque o esquema é JSON Schema 2020-12.
/// </remarks>
internal sealed class ComponenteDoEsquemaPublicado : IOpenApiSchema
{
    private const string SoVersao31 = "O esquema do documento de configuração é JSON Schema 2020-12 e só se escreve na versão 3.1 da OpenAPI.";

    private readonly JsonNode _conteudo;

    public ComponenteDoEsquemaPublicado(JsonNode conteudo) => _conteudo = conteudo;

    /// <summary>Ausente: o título está no JSON escrito, e o modelo não o repete.</summary>
    public string? Title => null;

    /// <summary>Ausente: a descrição está no JSON escrito, e o modelo não a repete.</summary>
    public string? Description
    {
        get => null;
        set => throw new NotSupportedException("O componente do esquema do documento de configuração é escrito como o esquema o declara e não aceita alteração.");
    }

    /// <summary>Ausente: o <c>$id</c> não entra no componente, porque trocaria a URI base das referências.</summary>
    public Uri? Schema => null;

    public string? Id => null;

    public string? Comment => null;

    public IDictionary<string, bool>? Vocabulary => null;

    public string? DynamicRef => null;

    public string? DynamicAnchor => null;

    public IDictionary<string, IOpenApiSchema>? Definitions => null;

    public string? ExclusiveMaximum => null;

    public string? ExclusiveMinimum => null;

    /// <summary>Ausente: o leitor da biblioteca reordena o <c>type</c> em lista e o conteúdo é escrito na ordem declarada.</summary>
    public JsonSchemaType? Type => null;

    /// <summary>Ausente: a biblioteca guarda a <c>const</c> como texto, e uma <c>const</c> numérica ou booleana mudaria de tipo.</summary>
    public string? Const => null;

    public string? Format => null;

    public string? Maximum => null;

    public string? Minimum => null;

    public int? MaxLength => null;

    public int? MinLength => null;

    public string? Pattern => null;

    public decimal? MultipleOf => null;

    public JsonNode? Default => null;

    public bool ReadOnly => false;

    public bool WriteOnly => false;

    public IList<IOpenApiSchema>? AllOf => null;

    public IList<IOpenApiSchema>? OneOf => null;

    public IList<IOpenApiSchema>? AnyOf => null;

    public IOpenApiSchema? Not => null;

    public ISet<string>? Required => null;

    public IOpenApiSchema? Items => null;

    public int? MaxItems => null;

    public int? MinItems => null;

    public bool? UniqueItems => null;

    /// <summary>Ausente: a biblioteca apaga o esquema booleano <c>false</c> de uma propriedade, e a proibição sumiria.</summary>
    public IDictionary<string, IOpenApiSchema>? Properties => null;

    public IDictionary<string, IOpenApiSchema>? PatternProperties => null;

    public int? MaxProperties => null;

    public int? MinProperties => null;

    public bool AdditionalPropertiesAllowed => true;

    public IOpenApiSchema? AdditionalProperties => null;

    public OpenApiDiscriminator? Discriminator => null;

    public JsonNode? Example => null;

    public IList<JsonNode>? Examples => null;

    public IList<JsonNode>? Enum => null;

    public bool UnevaluatedProperties => false;

    public OpenApiExternalDocs? ExternalDocs => null;

    public bool Deprecated => false;

    public OpenApiXml? Xml => null;

    public IDictionary<string, JsonNode>? UnrecognizedKeywords => null;

    public IDictionary<string, HashSet<string>>? DependentRequired => null;

    public IDictionary<string, IOpenApiExtension>? Extensions => null;

    public void SerializeAsV31(IOpenApiWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        Escrever(writer, _conteudo);
    }

    public void SerializeAsV3(IOpenApiWriter writer) =>
        throw new NotSupportedException(SoVersao31);

    public void SerializeAsV2(IOpenApiWriter writer) =>
        throw new NotSupportedException(SoVersao31);

    public IOpenApiSchema CreateShallowCopy() => new ComponenteDoEsquemaPublicado(_conteudo);

    // O número vai como o texto do JSON: o escritor da biblioteca formata os decimais pela cultura do escritor de texto
    // ("0,0001" em pt-BR), o que quebra o JSON e faria o contrato mudar de uma máquina para outra.
    private static void Escrever(IOpenApiWriter writer, JsonNode? no)
    {
        switch (no)
        {
            case JsonObject objeto:
                EscreverObjeto(writer, objeto);
                break;
            case JsonArray lista:
                EscreverLista(writer, lista);
                break;
            default:
                EscreverValor(writer, no);
                break;
        }
    }

    private static void EscreverObjeto(IOpenApiWriter writer, JsonObject objeto)
    {
        writer.WriteStartObject();
        foreach ((string nome, JsonNode? valor) in objeto)
        {
            writer.WritePropertyName(nome);
            Escrever(writer, valor);
        }

        writer.WriteEndObject();
    }

    private static void EscreverLista(IOpenApiWriter writer, JsonArray lista)
    {
        writer.WriteStartArray();
        foreach (JsonNode? item in lista)
        {
            Escrever(writer, item);
        }

        writer.WriteEndArray();
    }

    private static void EscreverValor(IOpenApiWriter writer, JsonNode? valor)
    {
        switch (valor?.GetValueKind())
        {
            case JsonValueKind.Number:
                writer.WriteRaw(valor.ToJsonString());
                break;
            case JsonValueKind.String:
                writer.WriteValue(valor.GetValue<string>());
                break;
            default:
                EscreverLogicoOuNulo(writer, valor);
                break;
        }
    }

    private static void EscreverLogicoOuNulo(IOpenApiWriter writer, JsonNode? valor)
    {
        if (valor?.GetValueKind() is JsonValueKind.True or JsonValueKind.False)
        {
            writer.WriteValue(valor.GetValue<bool>());
        }
        else
        {
            writer.WriteNull();
        }
    }
}
