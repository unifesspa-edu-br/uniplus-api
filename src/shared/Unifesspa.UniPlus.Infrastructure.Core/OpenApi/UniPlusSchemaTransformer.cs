namespace Unifesspa.UniPlus.Infrastructure.Core.OpenApi;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

/// <summary>
/// Schema transformer que aplica invariantes de domínio Uni+ a propriedades
/// tipadas. Hoje cobre <c>cpf</c> (regex de 11 dígitos + nota PII).
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

        // JsonSchemaType é [Flags] — propriedades nullable saem como
        // String | Null, então comparação por igualdade exata pula schemas
        // legítimos. HasFlag pega ambos os casos (String puro e String|Null).
        if (!schema.Type.HasValue || !schema.Type.Value.HasFlag(JsonSchemaType.String))
            return Task.CompletedTask;

        string? propertyName = context.JsonPropertyInfo?.Name;
        if (propertyName is null)
            return Task.CompletedTask;

        if (string.Equals(propertyName, "cpf", StringComparison.Ordinal))
        {
            schema.Pattern = @"^\d{11}$";
            schema.Description ??= "CPF (apenas dígitos, sem formatação). PII — sempre mascarar em logs ('***.***.***-XX', ADR-0011).";
        }

        return Task.CompletedTask;
    }
}
