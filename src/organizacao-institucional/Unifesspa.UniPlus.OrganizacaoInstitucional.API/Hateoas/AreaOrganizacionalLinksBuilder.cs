namespace Unifesspa.UniPlus.OrganizacaoInstitucional.API.Hateoas;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using Unifesspa.UniPlus.Infrastructure.Core.Hateoas;
using Unifesspa.UniPlus.OrganizacaoInstitucional.API.Controllers;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.DTOs;

/// <summary>
/// Builder de <c>_links</c> hypermedia (HATEOAS Level 1, ADR-0029/0049) para
/// <see cref="AreaOrganizacionalDto"/>.
/// </summary>
/// <remarks>
/// <para>
/// Relações em V1:
/// </para>
/// <list type="bullet">
///   <item><description><c>self</c> — URI canônica do recurso (sempre).</description></item>
///   <item><description><c>collection</c> — URI da listagem <c>/api/areas-organizacionais</c> (sempre).</description></item>
/// </list>
/// <para>
/// Action links (<c>desativar</c>, etc.) <strong>nunca</strong> aparecem aqui
/// (ADR-0029 §"Esta ADR não decide"); operações de mutação são descobertas
/// via OpenAPI (ADR-0030).
/// </para>
/// </remarks>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via IServiceProvider.AddSingleton<IResourceLinksBuilder<AreaOrganizacionalDto>, AreaOrganizacionalLinksBuilder>().")]
internal sealed class AreaOrganizacionalLinksBuilder : IResourceLinksBuilder<AreaOrganizacionalDto>
{
    private readonly LinkGenerator _linkGenerator;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AreaOrganizacionalLinksBuilder(LinkGenerator linkGenerator, IHttpContextAccessor httpContextAccessor)
    {
        ArgumentNullException.ThrowIfNull(linkGenerator);
        ArgumentNullException.ThrowIfNull(httpContextAccessor);
        _linkGenerator = linkGenerator;
        _httpContextAccessor = httpContextAccessor;
    }

    public IReadOnlyDictionary<string, string> Build(AreaOrganizacionalDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        // GetPathByAction com HttpContext respeita PathBase ambient (proxy reverso,
        // app.UsePathBase). Fora de request scope (jobs/webhooks futuros), cai num
        // path sem PathBase — correto. Path sempre relativo (sem scheme/host),
        // alinhado com ADR-0029 §"URIs relativas".
        HttpContext? httpContext = _httpContextAccessor.HttpContext;
        string controllerName = ControllerNameWithoutSuffix();

        string self = ResolverPath(httpContext, nameof(AreasOrganizacionaisController.ObterPorCodigo), controllerName, new { codigo = dto.Codigo });
        string collection = ResolverPath(httpContext, nameof(AreasOrganizacionaisController.Listar), controllerName, values: null);

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

    private static string ControllerNameWithoutSuffix()
    {
        const string suffix = "Controller";
        string name = nameof(AreasOrganizacionaisController);
        return name.EndsWith(suffix, StringComparison.Ordinal)
            ? name[..^suffix.Length]
            : name;
    }
}
