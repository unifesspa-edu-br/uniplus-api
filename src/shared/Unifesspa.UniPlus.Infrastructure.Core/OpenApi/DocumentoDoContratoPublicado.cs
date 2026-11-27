namespace Unifesspa.UniPlus.Infrastructure.Core.OpenApi;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Restringe o nome do documento de contrato, na rota, aos documentos que o serviço publica. Outro nome não chega à
/// operação e recebe a resposta comum do caminho que o serviço não oferece, sem repetir o nome recebido.
/// </summary>
public sealed class DocumentoDoContratoPublicado : IRouteConstraint
{
    public const string Restricao = "documentoPublicado";
    public const string Rota = "/openapi/{documentName:" + Restricao + "}.json";

    public bool Match(
        HttpContext? httpContext,
        IRouter? route,
        string routeKey,
        RouteValueDictionary values,
        RouteDirection routeDirection)
    {
        ArgumentNullException.ThrowIfNull(values);

        return httpContext is not null
            && values.TryGetValue(routeKey, out object? valor)
            && valor is string nome
            && httpContext.RequestServices.GetRequiredService<IServiceProviderIsKeyedService>()
                .IsKeyedService(typeof(IOpenApiDocumentProvider), nome);
    }
}
