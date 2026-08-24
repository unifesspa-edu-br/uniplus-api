namespace Unifesspa.UniPlus.Infrastructure.Core.OpenApi;

using System.Reflection;
using System.Text.Json.Nodes;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

/// <summary>
/// Schema transformer que aplica invariantes de domínio Uni+ a propriedades
/// tipadas. Hoje cobre <c>cpf</c> (regex de 11 dígitos + nota PII) e
/// <c>valoresSelecionaveis</c> (cardinalidade mínima 1, issue #1077).
/// <para>
/// O <c>code</c> de ProblemDetails NÃO é coberto aqui: o campo vive em
/// <c>ProblemDetails.Extensions["code"]</c> (<c>[JsonExtensionData]</c>) e
/// não recebe um <c>JsonPropertyInfo</c> nomeado — o pattern
/// <c>^[a-z]+(\.[a-z_]+)+$</c> da taxonomia (ADR-0023) é validado a partir
/// do spec gerado pela rule Spectral <c>uniplus-error-code-format</c>.
/// </para>
/// </summary>
public sealed class UniPlusSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(context);

        // Enum-as-string sem `type` explícito: o ASP.NET Core OpenAPI generator
        // emite `enum: [...]` quando JsonStringEnumConverter está registrado,
        // mas não popula `type: "string"`. Sem o type, geradores de cliente
        // (openapi-typescript, openapi-generator, NSwag) podem tratar a schema
        // como `any`, perdendo type safety. Inferimos string sempre que houver
        // enum sem type — invariante OpenAPI 3.1.
        if (schema.Enum is { Count: > 0 } && !schema.Type.HasValue)
        {
            schema.Type = JsonSchemaType.String;
        }

        string? propertyName = context.JsonPropertyInfo?.Name;
        if (propertyName is null)
            return Task.CompletedTask;

        AplicarVocabularioFechado(schema, context);

        // JsonSchemaType é [Flags] — propriedades nullable saem com o flag Null somado ao
        // tipo base (ex.: Array | Null, String | Null), então comparação por igualdade exata
        // pula schemas legítimos. HasFlag pega o tipo base com ou sem nulidade.
        if (schema.Type is { } tipo && tipo.HasFlag(JsonSchemaType.Array)
            && string.Equals(propertyName, "valoresSelecionaveis", StringComparison.Ordinal))
        {
            // issue #1077: um seletor de SELECAO_UNICA/SELECAO_MULTIPLA publicado sem opção
            // nenhuma não é respondível — o contrato exige pelo menos um valor selecionável
            // quando o array está presente (null continua válido para BOOLEANO/NUMERO).
            schema.MinItems = 1;
        }

        if (!schema.Type.HasValue || !schema.Type.Value.HasFlag(JsonSchemaType.String))
            return Task.CompletedTask;

        if (string.Equals(propertyName, "cpf", StringComparison.Ordinal))
        {
            schema.Pattern = @"^\d{11}$";
            schema.Description ??= "CPF (apenas dígitos, sem formatação). PII — sempre mascarar em logs ('***.999.999-**', ADR-0011).";
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Publica como <c>enum</c> o vocabulário que a propriedade declara via
    /// <see cref="VocabularioFechadoAttribute"/>. Num array, o conjunto pertence ao ITEM,
    /// não à lista: aplicá-lo à lista diria que a lista inteira é um dos valores.
    /// </summary>
    private static void AplicarVocabularioFechado(OpenApiSchema schema, OpenApiSchemaTransformerContext context)
    {
        if (context.JsonPropertyInfo?.AttributeProvider
                ?.GetCustomAttributes(typeof(VocabularioFechadoAttribute), inherit: true)
                .FirstOrDefault() is not VocabularioFechadoAttribute vocabulario
            || vocabulario.Valores.Count == 0)
        {
            return;
        }

        // JsonSchemaType é [Flags]: uma propriedade anulável sai com Null somado ao tipo
        // base, então a comparação precisa ser por flag e não por igualdade.
        bool ehArray = schema.Type is { } tipo && tipo.HasFlag(JsonSchemaType.Array);
        IOpenApiSchema? alvo = ehArray ? schema.Items : schema;

        if (alvo is not OpenApiSchema destino)
        {
            return;
        }

        destino.Type ??= JsonSchemaType.String;
        destino.Enum = [.. vocabulario.Valores.Select(static v => (JsonNode)JsonValue.Create(v)!)];
    }
}
