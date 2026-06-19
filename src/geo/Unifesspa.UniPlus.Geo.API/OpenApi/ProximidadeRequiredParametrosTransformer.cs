namespace Unifesspa.UniPlus.Geo.API.OpenApi;

using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

using Unifesspa.UniPlus.Geo.API.Controllers;

/// <summary>
/// Operation transformer que marca como <c>required</c> no contrato OpenAPI os
/// parâmetros de query obrigatórios da consulta de proximidade (#678): <c>lat</c>,
/// <c>long</c> e <c>raioKm</c>. Eles são validados no boundary
/// (<see cref="Formatting.ConsultaProximidade"/>) — ausência → 400 — mas, como o
/// ApiExplorer descreve query params <c>double?</c> como opcionais, sem este transformer
/// o contrato (e os clientes gerados a partir dele) os trataria como omissíveis e o erro
/// só apareceria em runtime. <c>limit</c> permanece opcional (tem default).
/// </summary>
internal sealed class ProximidadeRequiredParametrosTransformer : IOpenApiOperationTransformer
{
    private static readonly string[] ParametrosObrigatorios = ["lat", "long", "raioKm"];

    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        // Só as operações do ProximidadeController — não toca os demais endpoints do Geo.
        if (context.Description.ActionDescriptor is not ControllerActionDescriptor descriptor
            || descriptor.ControllerTypeInfo.AsType() != typeof(ProximidadeController))
        {
            return Task.CompletedTask;
        }

        if (operation.Parameters is null)
        {
            return Task.CompletedTask;
        }

        IEnumerable<OpenApiParameter> obrigatorios = operation.Parameters
            .OfType<OpenApiParameter>()
            .Where(p => p.In == ParameterLocation.Query
                     && ParametrosObrigatorios.Contains(p.Name, StringComparer.Ordinal));

        foreach (OpenApiParameter parametro in obrigatorios)
        {
            parametro.Required = true;
        }

        return Task.CompletedTask;
    }
}
