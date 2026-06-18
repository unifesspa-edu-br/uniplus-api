namespace Unifesspa.UniPlus.Geo.API.Hateoas;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

/// <summary>
/// Resolve URIs relativas (sem scheme/host) de actions dos controllers do Geo via
/// <see cref="LinkGenerator"/>, para os <c>_links</c> hypermedia (HATEOAS Level 1,
/// ADR-0029). Centraliza a derivação por route template e a regra de "controller
/// sem sufixo" usada pelos builders de Estado e Cidade.
/// </summary>
internal static class GeoLinkPathResolver
{
    private const string ControllerSuffix = "Controller";

    /// <summary>Nome do controller sem o sufixo <c>Controller</c> (ex.: <c>EstadosController</c> → <c>Estados</c>).</summary>
    public static string ControllerName(string fullControllerName) =>
        fullControllerName.EndsWith(ControllerSuffix, StringComparison.Ordinal)
            ? fullControllerName[..^ControllerSuffix.Length]
            : fullControllerName;

    /// <summary>
    /// Resolve o path da action; lança se a rota não for resolvível (config de
    /// rota inconsistente — falha cedo em vez de emitir link quebrado).
    /// </summary>
    public static string Resolver(
        LinkGenerator linkGenerator,
        HttpContext? httpContext,
        string action,
        string controller,
        object? values)
    {
        string? path = httpContext is not null
            ? linkGenerator.GetPathByAction(httpContext, action: action, controller: controller, values: values)
            : linkGenerator.GetPathByAction(action: action, controller: controller, values: values);

        return path
            ?? throw new InvalidOperationException(
                $"LinkGenerator não conseguiu resolver a rota para {controller}.{action}. " +
                "Verifique o registro do controller e o template de rota.");
    }
}
