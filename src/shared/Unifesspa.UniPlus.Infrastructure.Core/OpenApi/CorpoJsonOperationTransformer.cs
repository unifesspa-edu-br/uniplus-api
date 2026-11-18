namespace Unifesspa.UniPlus.Infrastructure.Core.OpenApi;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

/// <summary>
/// Escreve o <c>requestBody</c> <c>application/json</c> obrigatório das actions marcadas com <see cref="CorpoJsonAttribute"/>:
/// uma referência ao componente que descreve o corpo.
/// </summary>
public sealed class CorpoJsonOperationTransformer : IOpenApiOperationTransformer
{
    private const string MediaType = "application/json";

    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        CorpoJsonAttribute? corpo = context.Description.ActionDescriptor.EndpointMetadata
            .OfType<CorpoJsonAttribute>()
            .FirstOrDefault();
        if (corpo is not null)
        {
            operation.RequestBody = RequestBodyDe(corpo, context.Document);
        }

        return Task.CompletedTask;
    }

    private static OpenApiRequestBody RequestBodyDe(CorpoJsonAttribute corpo, OpenApiDocument? documento) => new()
    {
        Required = true,
        Content = new Dictionary<string, OpenApiMediaType>(StringComparer.Ordinal)
        {
            [MediaType] = new OpenApiMediaType { Schema = new OpenApiSchemaReference(corpo.Componente, documento) },
        },
    };
}
