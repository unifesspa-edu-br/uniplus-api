namespace Unifesspa.UniPlus.Infrastructure.Core.OpenApi;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

/// <summary>
/// Escreve o <c>requestBody</c> <c>multipart/form-data</c> das actions marcadas com
/// <see cref="CorpoMultipartAttribute"/>: o arquivo como <c>string</c> de formato <c>binary</c> e os
/// campos de texto como <c>string</c>, todos obrigatórios.
/// </summary>
public sealed class CorpoMultipartOperationTransformer : IOpenApiOperationTransformer
{
    private const string MediaType = "multipart/form-data";

    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        CorpoMultipartAttribute? corpo = context.Description.ActionDescriptor.EndpointMetadata
            .OfType<CorpoMultipartAttribute>()
            .FirstOrDefault();
        if (corpo is null)
        {
            return Task.CompletedTask;
        }

        Dictionary<string, IOpenApiSchema> propriedades = new(StringComparer.Ordinal)
        {
            [corpo.CampoDoArquivo] = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" },
        };
        foreach (string campo in corpo.CamposDeTexto)
        {
            propriedades[campo] = new OpenApiSchema { Type = JsonSchemaType.String };
        }

        operation.RequestBody = new OpenApiRequestBody
        {
            Required = true,
            Content = new Dictionary<string, OpenApiMediaType>(StringComparer.Ordinal)
            {
                [MediaType] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.Object,
                        Properties = propriedades,
                        Required = new HashSet<string>([corpo.CampoDoArquivo, .. corpo.CamposDeTexto], StringComparer.Ordinal),
                    },
                },
            },
        };

        return Task.CompletedTask;
    }
}
