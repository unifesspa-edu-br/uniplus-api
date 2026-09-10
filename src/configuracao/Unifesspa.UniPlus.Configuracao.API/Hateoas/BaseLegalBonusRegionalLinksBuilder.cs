namespace Unifesspa.UniPlus.Configuracao.API.Hateoas;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using Unifesspa.UniPlus.Configuracao.API.Controllers;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Infrastructure.Core.Hateoas;

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instanciada via DI.")]
internal sealed class BaseLegalBonusRegionalLinksBuilder : IResourceLinksBuilder<BaseLegalBonusRegionalDto>
{
    private readonly LinkGenerator _linkGenerator;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public BaseLegalBonusRegionalLinksBuilder(LinkGenerator linkGenerator, IHttpContextAccessor httpContextAccessor)
    {
        ArgumentNullException.ThrowIfNull(linkGenerator);
        ArgumentNullException.ThrowIfNull(httpContextAccessor);
        _linkGenerator = linkGenerator;
        _httpContextAccessor = httpContextAccessor;
    }

    public IReadOnlyDictionary<string, string> Build(BaseLegalBonusRegionalDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        HttpContext? httpContext = _httpContextAccessor.HttpContext;
        const string controllerName = "BaseLegalBonusRegional";

        string self = ResolverPath(
            httpContext, nameof(BaseLegalBonusRegionalController.ObterPorId), controllerName, new { id = dto.Id });
        string collection = ResolverPath(
            httpContext, nameof(BaseLegalBonusRegionalController.Listar), controllerName, values: null);

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
                $"LinkGenerator não conseguiu resolver a rota para {action}.");
    }
}
