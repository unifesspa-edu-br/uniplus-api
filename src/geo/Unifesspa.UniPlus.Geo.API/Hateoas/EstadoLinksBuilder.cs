namespace Unifesspa.UniPlus.Geo.API.Hateoas;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using Unifesspa.UniPlus.Geo.API.Controllers;
using Unifesspa.UniPlus.Geo.Application.DTOs;
using Unifesspa.UniPlus.Infrastructure.Core.Hateoas;

/// <summary>
/// Builder de <c>_links</c> hypermedia (HATEOAS Level 1, ADR-0029) para
/// <see cref="EstadoDto"/>. Relações: <c>self</c> (<c>GET /api/estados/{uf}</c>),
/// <c>cidades</c> (lista de cidades da UF, <c>GET /api/cidades?uf={uf}</c>) e
/// <c>collection</c> (<c>GET /api/estados</c>).
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via AddSingleton<IResourceLinksBuilder<EstadoDto>, EstadoLinksBuilder>().")]
internal sealed class EstadoLinksBuilder : IResourceLinksBuilder<EstadoDto>
{
    private static readonly string EstadosControllerNome = GeoLinkPathResolver.ControllerName(nameof(Controllers.EstadosController));
    private static readonly string CidadesControllerNome = GeoLinkPathResolver.ControllerName(nameof(Controllers.CidadesController));

    private readonly LinkGenerator _linkGenerator;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public EstadoLinksBuilder(LinkGenerator linkGenerator, IHttpContextAccessor httpContextAccessor)
    {
        ArgumentNullException.ThrowIfNull(linkGenerator);
        ArgumentNullException.ThrowIfNull(httpContextAccessor);
        _linkGenerator = linkGenerator;
        _httpContextAccessor = httpContextAccessor;
    }

    public IReadOnlyDictionary<string, string> Build(EstadoDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        HttpContext? httpContext = _httpContextAccessor.HttpContext;

        string self = GeoLinkPathResolver.Resolver(
            _linkGenerator, httpContext, nameof(Controllers.EstadosController.ObterPorUf), EstadosControllerNome, new { uf = dto.Uf });
        string collection = GeoLinkPathResolver.Resolver(
            _linkGenerator, httpContext, nameof(Controllers.EstadosController.Listar), EstadosControllerNome, values: null);

        // A lista de cidades da UF é o endpoint de cidades com o filtro uf= anexado
        // (uf é query param, não rota — o LinkGenerator não a inclui em values).
        string cidadesBase = GeoLinkPathResolver.Resolver(
            _linkGenerator, httpContext, nameof(Controllers.CidadesController.Listar), CidadesControllerNome, values: null);
        string cidades = $"{cidadesBase}?uf={Uri.EscapeDataString(dto.Uf)}";

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["self"] = self,
            ["cidades"] = cidades,
            ["collection"] = collection,
        };
    }
}
