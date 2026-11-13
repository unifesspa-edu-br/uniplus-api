namespace Unifesspa.UniPlus.Infrastructure.Core.OpenApi;

using System.Text.Json.Serialization.Metadata;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

/// <summary>
/// Publica o instante como texto <c>date-time</c>. O instante tem conversor JSON próprio, e o gerador, que não infere
/// a forma do que um conversor escreve, deixa o schema sem <c>type</c> e a lista ou o dicionário sem o schema do item.
/// O instante é anulável só quando o tipo é e quando não é um parâmetro de rota, query ou cabeçalho, que o gerador
/// publica sem a variante nula.
/// </summary>
public sealed class InstanteSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(context);

        PublicarInstante(schema, context);

        return Task.CompletedTask;
    }

    private static void PublicarInstante(OpenApiSchema schema, OpenApiSchemaTransformerContext context)
    {
        JsonTypeInfo tipo = context.JsonTypeInfo;
        bool semVarianteNula = context.ParameterDescription is not null && context.JsonPropertyInfo is null;

        if (EhInstante(tipo.Type))
        {
            PublicarComoTextoDateTime(schema, tipo.Type, semVarianteNula);
        }
        else if (tipo.ElementType is { } tipoDoItem && EhInstante(tipoDoItem))
        {
            PublicarItem(schema, tipo.Kind, tipoDoItem, semVarianteNula);
        }
    }

    private static bool EhInstante(Type tipo) => (Nullable.GetUnderlyingType(tipo) ?? tipo) == typeof(DateTimeOffset);

    private static void PublicarComoTextoDateTime(OpenApiSchema schema, Type tipo, bool semVarianteNula)
    {
        bool anulavel = Nullable.GetUnderlyingType(tipo) is not null && !semVarianteNula;

        schema.Type = anulavel ? JsonSchemaType.String | JsonSchemaType.Null : JsonSchemaType.String;
        schema.Format = "date-time";
    }

    private static void PublicarItem(OpenApiSchema colecao, JsonTypeInfoKind forma, Type tipoDoItem, bool semVarianteNula)
    {
        OpenApiSchema item = new();
        PublicarComoTextoDateTime(item, tipoDoItem, semVarianteNula);

        if (forma == JsonTypeInfoKind.Enumerable)
        {
            colecao.Items = item;
        }
        else if (forma == JsonTypeInfoKind.Dictionary)
        {
            colecao.AdditionalProperties = item;
        }
    }
}
