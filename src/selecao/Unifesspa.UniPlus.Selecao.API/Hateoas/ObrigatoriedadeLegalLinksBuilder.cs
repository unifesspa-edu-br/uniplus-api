namespace Unifesspa.UniPlus.Selecao.API.Hateoas;

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using Unifesspa.UniPlus.Infrastructure.Core.Hateoas;
using Unifesspa.UniPlus.Selecao.API.Controllers;
using Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>
/// Constrói <c>_links</c> hypermedia (HATEOAS Level 1, ADR-0029 + ADR-0049)
/// para <see cref="ObrigatoriedadeLegalDto"/>. Action links (publicar,
/// desativar, etc.) NÃO entram aqui — são descobertos via OpenAPI (ADR-0030).
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via IServiceProvider.AddSingleton<IResourceLinksBuilder<ObrigatoriedadeLegalDto>, ...>().")]
internal sealed class ObrigatoriedadeLegalLinksBuilder : IResourceLinksBuilder<ObrigatoriedadeLegalDto>
{
    private readonly LinkGenerator _linkGenerator;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ObrigatoriedadeLegalLinksBuilder(
        LinkGenerator linkGenerator,
        IHttpContextAccessor httpContextAccessor)
    {
        ArgumentNullException.ThrowIfNull(linkGenerator);
        ArgumentNullException.ThrowIfNull(httpContextAccessor);
        _linkGenerator = linkGenerator;
        _httpContextAccessor = httpContextAccessor;
    }

    public IReadOnlyDictionary<string, string> Build(ObrigatoriedadeLegalDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        HttpContext? httpContext = _httpContextAccessor.HttpContext;
        const string controllerName = "ObrigatoriedadeLegal";

        string self = ResolverPath(
            httpContext,
            nameof(ObrigatoriedadeLegalController.ObterPorId),
            controllerName,
            new { id = dto.Id });
        string collection = ResolverPath(
            httpContext,
            nameof(ObrigatoriedadeLegalController.Listar),
            controllerName,
            values: null);

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["self"] = self,
            ["collection"] = collection,
        };
    }

    private string ResolverPath(HttpContext? httpContext, string action, string controller, object? values)
    {
        string? path = httpContext is not null
            ? _linkGenerator.GetPathByAction(httpContext, action: action, controller: controller, values: values)
            : _linkGenerator.GetPathByAction(action: action, controller: controller, values: values);

        return path
            ?? throw new InvalidOperationException(
                $"LinkGenerator não conseguiu resolver a rota para {action}. " +
                "Verifique o registro do controller e o template de rota.");
    }
}
