namespace Unifesspa.UniPlus.Infrastructure.Core.OpenApi;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

/// <summary>
/// Escreve o corpo <c>application/json</c> da resposta <c>200</c> das actions marcadas com <see cref="RespostaJsonAttribute"/>:
/// uma referência ao componente que descreve o corpo. Precisa rodar antes do <see cref="VendorMediaTypeOperationTransformer"/>,
/// que copia o schema desse corpo para as vendor MIME da operação.
/// </summary>
public sealed class RespostaJsonOperationTransformer : IOpenApiOperationTransformer
{
    private const string MediaType = "application/json";
    private const string StatusDeSucesso = "200";

    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        RespostaJsonAttribute? resposta = context.Description.ActionDescriptor.EndpointMetadata
            .OfType<RespostaJsonAttribute>()
            .FirstOrDefault();
        if (resposta is not null)
        {
            RespostaDeSucesso(operation).Content = ConteudoDe(resposta, context.Document);
        }

        return Task.CompletedTask;
    }

    private static Dictionary<string, OpenApiMediaType> ConteudoDe(RespostaJsonAttribute resposta, OpenApiDocument? documento) => new(StringComparer.Ordinal)
    {
        [MediaType] = new OpenApiMediaType { Schema = new OpenApiSchemaReference(resposta.Componente, documento) },
    };

    private static OpenApiResponse RespostaDeSucesso(OpenApiOperation operation)
    {
        operation.Responses ??= [];
        if (operation.Responses.TryGetValue(StatusDeSucesso, out IOpenApiResponse? existente) && existente is OpenApiResponse concreta)
        {
            return concreta;
        }

        OpenApiResponse nova = new() { Description = "OK" };
        operation.Responses[StatusDeSucesso] = nova;
        return nova;
    }
}
