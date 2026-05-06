namespace Unifesspa.UniPlus.Selecao.API.Hateoas;

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using Unifesspa.UniPlus.Infrastructure.Core.Hateoas;
using Unifesspa.UniPlus.Selecao.API.Controllers;
using Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>
/// Constrói o conjunto de <c>_links</c> hypermedia (HATEOAS Level 1) para
/// <see cref="EditalDto"/>, conforme
/// <see href="https://github.com/unifesspa-edu-br/uniplus-api/blob/main/docs/adrs/0029-hateoas-level-1-links.md">ADR-0029</see>.
/// </summary>
/// <remarks>
/// <para>
/// Relações canônicas emitidas em V1:
/// </para>
/// <list type="bullet">
///   <item><description><c>self</c> — URI canônica do próprio edital (sempre).</description></item>
///   <item><description><c>collection</c> — URI da coleção <c>/api/editais</c> (sempre — útil para back-nav).</description></item>
/// </list>
/// <para>
/// Relações futuras (<c>inscricoes</c>, <c>classificacao</c>) entram quando
/// os endpoints correspondentes existirem — issue dedicada por recurso.
/// Action links (<c>publicar</c> etc.) <strong>nunca</strong> aparecem aqui
/// (ADR-0029 §"Esta ADR não decide"); são descobertos via OpenAPI (ADR-0030).
/// </para>
/// <para>
/// URIs são <strong>relativas</strong> à raiz da API (ADR-0029 §"URIs
/// relativas"). Geração via <see cref="LinkGenerator"/> (singleton DI), o que
/// blinda o builder contra alterações no <c>Route</c> attribute do controller
/// — refator de URL não quebra os links.
/// </para>
/// </remarks>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via IServiceProvider.AddSingleton<IResourceLinksBuilder<EditalDto>, EditalLinksBuilder>().")]
internal sealed class EditalLinksBuilder : IResourceLinksBuilder<EditalDto>
{
    private readonly LinkGenerator _linkGenerator;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public EditalLinksBuilder(LinkGenerator linkGenerator, IHttpContextAccessor httpContextAccessor)
    {
        ArgumentNullException.ThrowIfNull(linkGenerator);
        ArgumentNullException.ThrowIfNull(httpContextAccessor);
        _linkGenerator = linkGenerator;
        _httpContextAccessor = httpContextAccessor;
    }

    public IReadOnlyDictionary<string, string> Build(EditalDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        // GetPathByAction com HttpContext respeita o ambient PathBase
        // (deployments atrás de reverse proxy ou app.UsePathBase("/foo")
        // emitem links começando em /foo/api/editais...). Sem HttpContext
        // (cenário futuro: jobs/webhooks invocando o builder fora de request
        // scope), cai num path sem PathBase — correto para esses contextos.
        // Em ambos os casos, o path é relativo (sem scheme/host) — alinhado
        // com a invariante "URIs relativas à raiz da API" da ADR-0029.
        HttpContext? httpContext = _httpContextAccessor.HttpContext;
        string controllerName = ControllerNameWithoutSuffix();

        string self = ResolverPath(httpContext, nameof(EditalController.ObterPorId), controllerName, new { id = dto.Id });
        string collection = ResolverPath(httpContext, nameof(EditalController.Listar), controllerName, values: null);

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

    /// <summary>
    /// Convenção do MVC: o nome do controller usado no roteamento é o nome
    /// da classe sem o sufixo <c>Controller</c>. Centralizar aqui evita
    /// string literal duplicada e permite refator no controller sem quebrar
    /// o builder.
    /// </summary>
    private static string ControllerNameWithoutSuffix()
    {
        const string suffix = "Controller";
        string name = nameof(EditalController);
        return name.EndsWith(suffix, StringComparison.Ordinal)
            ? name[..^suffix.Length]
            : name;
    }
}
