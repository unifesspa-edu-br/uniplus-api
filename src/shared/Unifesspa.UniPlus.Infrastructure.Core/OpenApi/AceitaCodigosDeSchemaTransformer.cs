namespace Unifesspa.UniPlus.Infrastructure.Core.OpenApi;

using System.Text.Json.Serialization.Metadata;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

/// <summary>
/// Declara o campo de pedido marcado com <see cref="AceitaCodigosDeAttribute{TEnum}"/> para que o contrato o publique como
/// referência ao componente dos códigos do enum, em vez de texto livre. A referência entra no documento pronto, por
/// <see cref="ComponentesDosCodigos"/>: o gerador troca por componente inteiro a referência que um schema leva.
/// </summary>
internal sealed class AceitaCodigosDeSchemaTransformer : IOpenApiSchemaTransformer
{
    private readonly ComponentesDosCodigos _codigos;

    public AceitaCodigosDeSchemaTransformer(ComponentesDosCodigos codigos)
    {
        ArgumentNullException.ThrowIfNull(codigos);

        _codigos = codigos;
    }

    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.JsonTypeInfo.Kind == JsonTypeInfoKind.Object)
        {
            foreach (JsonPropertyInfo propriedade in context.JsonTypeInfo.Properties)
            {
                if (CampoQueAceitaCodigos.De(propriedade) is { } campo)
                {
                    _codigos.Registrar(context.JsonTypeInfo, propriedade.Name, campo);
                }
            }
        }

        return Task.CompletedTask;
    }
}
