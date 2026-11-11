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
/// para <see cref="MotivoDeDecisaoDto"/>. Action links (reativar,
/// desativar, etc.) NÃO entram aqui — são descobertos via OpenAPI (ADR-0030).
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via IServiceProvider.AddSingleton<IResourceLinksBuilder<MotivoDeDecisaoDto>, ...>().")]
internal sealed class MotivoDeDecisaoLinksBuilder : IResourceLinksBuilder<MotivoDeDecisaoDto>
{
    private readonly LinkGenerator _linkGenerator;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public MotivoDeDecisaoLinksBuilder(
        LinkGenerator linkGenerator,
        IHttpContextAccessor httpContextAccessor)
    {
        ArgumentNullException.ThrowIfNull(linkGenerator);
        ArgumentNullException.ThrowIfNull(httpContextAccessor);
        _linkGenerator = linkGenerator;
        _httpContextAccessor = httpContextAccessor;
    }

    public IReadOnlyDictionary<string, string> Build(MotivoDeDecisaoDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        HttpContext? httpContext = _httpContextAccessor.HttpContext;
        const string controllerName = "MotivosDeDecisao";

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["self"] = ResolverPath(httpContext, nameof(MotivosDeDecisaoController.ObterPorId), controllerName, new { id = dto.Id }),
            ["collection"] = ResolverPath(httpContext, nameof(MotivosDeDecisaoController.Listar), controllerName, values: null),
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
