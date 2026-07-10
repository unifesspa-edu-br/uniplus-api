namespace Unifesspa.UniPlus.Publicacoes.API.Hateoas;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using Unifesspa.UniPlus.Infrastructure.Core.Hateoas;
using Unifesspa.UniPlus.Publicacoes.API.Controllers;
using Unifesspa.UniPlus.Publicacoes.Application.DTOs;

/// <summary>
/// Builder de <c>_links</c> hypermedia (HATEOAS Level 1, ADR-0029) para
/// <see cref="AtoNormativoDto"/>. Relações em V1: <c>self</c> e <c>collection</c>.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via IServiceProvider.AddSingleton<IResourceLinksBuilder<AtoNormativoDto>, AtoNormativoLinksBuilder>().")]
internal sealed class AtoNormativoLinksBuilder : IResourceLinksBuilder<AtoNormativoDto>
{
    private const string ControllerName = "AtosNormativos";

    private readonly LinkGenerator _linkGenerator;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AtoNormativoLinksBuilder(LinkGenerator linkGenerator, IHttpContextAccessor httpContextAccessor)
    {
        ArgumentNullException.ThrowIfNull(linkGenerator);
        ArgumentNullException.ThrowIfNull(httpContextAccessor);
        _linkGenerator = linkGenerator;
        _httpContextAccessor = httpContextAccessor;
    }

    public IReadOnlyDictionary<string, string> Build(AtoNormativoDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        HttpContext? httpContext = _httpContextAccessor.HttpContext;

        string self = ResolverPath(
            httpContext, nameof(AtosNormativosController.ObterPorId), new { id = dto.Id });
        string collection = ResolverPath(
            httpContext, nameof(AtosNormativosController.Listar), values: null);

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["self"] = self,
            ["collection"] = collection,
        };
    }

    private string ResolverPath(HttpContext? httpContext, string action, object? values)
    {
        string? path = httpContext is not null
            ? _linkGenerator.GetPathByAction(httpContext, action: action, controller: ControllerName, values: values)
            : _linkGenerator.GetPathByAction(action: action, controller: ControllerName, values: values);

        return path
            ?? throw new InvalidOperationException(
                $"LinkGenerator não conseguiu resolver a rota para {action}. " +
                "Verifique o registro do controller e o template de rota.");
    }
}
