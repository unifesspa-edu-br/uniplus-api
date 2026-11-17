namespace Unifesspa.UniPlus.Infrastructure.Core.OpenApi;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

/// <summary>
/// Escreve o <c>requestBody</c> <c>application/json</c> obrigatório das actions marcadas com <see cref="CorpoJsonAttribute"/>:
/// um objeto cujo formato está no JSON Schema publicado, que a documentação externa do esquema aponta.
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
            operation.RequestBody = RequestBodyDe(corpo);
        }

        return Task.CompletedTask;
    }

    private static OpenApiRequestBody RequestBodyDe(CorpoJsonAttribute corpo) => new()
    {
        Required = true,
        Content = new Dictionary<string, OpenApiMediaType>(StringComparer.Ordinal)
        {
            [MediaType] = new OpenApiMediaType { Schema = ObjetoDescritoPeloEsquema(corpo.Esquema) },
        },
    };

    private static OpenApiSchema ObjetoDescritoPeloEsquema(string esquema) => new()
    {
        Type = JsonSchemaType.Object,
        Description = "O corpo é um documento JSON no formato do esquema indicado na documentação externa.",
        ExternalDocs = new OpenApiExternalDocs { Description = "JSON Schema do corpo.", Url = new Uri(esquema) },
    };
}
