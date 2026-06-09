namespace Unifesspa.UniPlus.OrganizacaoInstitucional.API.Hateoas;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using Unifesspa.UniPlus.Infrastructure.Core.Hateoas;
using Unifesspa.UniPlus.OrganizacaoInstitucional.API.Controllers;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.DTOs;

/// <summary>
/// Builder de <c>_links</c> hypermedia (HATEOAS Level 1, ADR-0029/0049) para
/// <see cref="InstituicaoDto"/>.
/// </summary>
/// <remarks>
/// Relação em V1: <c>self</c> (URI canônica do recurso singleton). Sem
/// <c>collection</c> — a Instituição não tem listagem (ADR-0055). Action links
/// nunca aparecem aqui (ADR-0029 §"Esta ADR não decide").
/// </remarks>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via IServiceProvider.AddSingleton<IResourceLinksBuilder<InstituicaoDto>, InstituicaoLinksBuilder>().")]
internal sealed class InstituicaoLinksBuilder : IResourceLinksBuilder<InstituicaoDto>
{
    private readonly LinkGenerator _linkGenerator;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public InstituicaoLinksBuilder(LinkGenerator linkGenerator, IHttpContextAccessor httpContextAccessor)
    {
        ArgumentNullException.ThrowIfNull(linkGenerator);
        ArgumentNullException.ThrowIfNull(httpContextAccessor);
        _linkGenerator = linkGenerator;
        _httpContextAccessor = httpContextAccessor;
    }

    public IReadOnlyDictionary<string, string> Build(InstituicaoDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        HttpContext? httpContext = _httpContextAccessor.HttpContext;
        string controllerName = NomeControllerSemSufixo();

        string self = ResolverPath(httpContext, nameof(InstituicaoController.Obter), controllerName, values: null);

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["self"] = self,
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

    private static string NomeControllerSemSufixo()
    {
        const string sufixo = "Controller";
        string nome = nameof(InstituicaoController);
        return nome.EndsWith(sufixo, StringComparison.Ordinal)
            ? nome[..^sufixo.Length]
            : nome;
    }
}
