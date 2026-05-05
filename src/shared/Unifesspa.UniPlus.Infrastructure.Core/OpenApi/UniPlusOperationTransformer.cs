namespace Unifesspa.UniPlus.Infrastructure.Core.OpenApi;

using System.Text.Json.Nodes;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

using Idempotency;

/// <summary>
/// Operation transformer que injeta metadata Uni+ por operação:
/// <list type="bullet">
///   <item><description>Header <c>Idempotency-Key</c> declarado como required quando a action tem <c>[RequiresIdempotencyKey]</c>.</description></item>
///   <item><description>Extension <c>x-uniplus-idempotent: true</c> em endpoints com idempotência opt-in.</description></item>
/// </list>
/// </summary>
public sealed class UniPlusOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        IList<object> metadata = context.Description.ActionDescriptor.EndpointMetadata;

        bool requiresIdempotency = metadata.OfType<RequiresIdempotencyKeyAttribute>().Any();
        if (requiresIdempotency)
        {
            operation.Parameters ??= [];
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "Idempotency-Key",
                In = ParameterLocation.Header,
                Required = true,
                Description = "Chave opaca (1-255 ASCII printable, sem ',' ou ';') para retry seguro do comando. "
                    + "Replay com mesma key + mesmo body retorna response cacheada (ADR-0027).",
                Schema = new OpenApiSchema
                {
                    Type = JsonSchemaType.String,
                    MinLength = 1,
                    MaxLength = 255,
                },
            });

            operation.Extensions ??= new Dictionary<string, IOpenApiExtension>(StringComparer.Ordinal);
            operation.Extensions["x-uniplus-idempotent"] = new JsonNodeExtension(JsonValue.Create(true));
        }

        return Task.CompletedTask;
    }
}
