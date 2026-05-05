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

        if (schema.Type != JsonSchemaType.String)
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
