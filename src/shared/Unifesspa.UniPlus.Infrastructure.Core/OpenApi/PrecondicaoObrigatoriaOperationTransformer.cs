namespace Unifesspa.UniPlus.Infrastructure.Core.OpenApi;

using System.Reflection;

using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

/// <summary>
/// Declara no OpenAPI que o <c>If-Match</c> é obrigatório nas rotas marcadas com <see cref="PrecondicaoObrigatoriaAttribute"/>.
/// </summary>
/// <remarks>
/// O <c>If-Match</c> declarado como <c>[FromHeader] string?</c> entra no spec como parâmetro opcional, e um parâmetro
/// opcional numa rota que não funciona sem ele convida o cliente a omiti-lo e receber 428, sem que o contrato avise.
/// </remarks>
public sealed class PrecondicaoObrigatoriaOperationTransformer : IOpenApiOperationTransformer
{
    private const string IfMatchParam = "If-Match";

    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        MethodInfo? metodo = (context.Description.ActionDescriptor as ControllerActionDescriptor)?.MethodInfo;
        if (metodo?.GetCustomAttribute<PrecondicaoObrigatoriaAttribute>(inherit: true) is not null)
        {
            MarcarIfMatchComoObrigatorio(operation);
        }

        return Task.CompletedTask;
    }

    private static void MarcarIfMatchComoObrigatorio(OpenApiOperation operation)
    {
        IEnumerable<OpenApiParameter> ifMatch = (operation.Parameters ?? [])
            .OfType<OpenApiParameter>()
            .Where(static p => p.In == ParameterLocation.Header
                && string.Equals(p.Name, IfMatchParam, StringComparison.OrdinalIgnoreCase));

        foreach (OpenApiParameter parametro in ifMatch)
        {
            parametro.Required = true;
            parametro.Description = "Precondição de concorrência (RFC 9110 §13.1.1) — o ETag da revisão lida. "
                + "OBRIGATÓRIO nesta rota: sem a precondição ela responde 428. "
                + "Comparação FORTE: uma weak tag (W/\"...\") nunca casa, e produz 412.";
        }
    }
}

/// <summary>
/// A rota <b>não funciona sem</b> <c>If-Match</c>: a ausência da precondição é falha de protocolo (428), não estado válido.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class PrecondicaoObrigatoriaAttribute : Attribute;
