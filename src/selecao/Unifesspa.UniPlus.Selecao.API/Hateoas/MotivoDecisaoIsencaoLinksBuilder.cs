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
/// para <see cref="MotivoDecisaoIsencaoDto"/>. Action links (ativar,
/// desativar, etc.) NÃO entram aqui — são descobertos via OpenAPI (ADR-0030).
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via IServiceProvider.AddSingleton<IResourceLinksBuilder<MotivoDecisaoIsencaoDto>, ...>().")]
internal sealed class MotivoDecisaoIsencaoLinksBuilder : IResourceLinksBuilder<MotivoDecisaoIsencaoDto>
{
    private readonly LinkGenerator _linkGenerator;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public MotivoDecisaoIsencaoLinksBuilder(
        LinkGenerator linkGenerator,
        IHttpContextAccessor httpContextAccessor)
    {
        ArgumentNullException.ThrowIfNull(linkGenerator);
        ArgumentNullException.ThrowIfNull(httpContextAccessor);
        _linkGenerator = linkGenerator;
        _httpContextAccessor = httpContextAccessor;
    }

    public IReadOnlyDictionary<string, string> Build(MotivoDecisaoIsencaoDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        HttpContext? httpContext = _httpContextAccessor.HttpContext;
        const string controllerName = "MotivosDecisaoIsencao";

        string self = ResolverPath(
            httpContext,
            nameof(MotivosDecisaoIsencaoController.ObterPorId),
            controllerName,
            new { id = dto.Id });
        string collection = ResolverPath(
            httpContext,
            nameof(MotivosDecisaoIsencaoController.Listar),
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
