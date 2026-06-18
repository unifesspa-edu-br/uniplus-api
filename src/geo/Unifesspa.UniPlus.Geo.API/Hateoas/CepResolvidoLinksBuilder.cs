namespace Unifesspa.UniPlus.Geo.API.Hateoas;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using Unifesspa.UniPlus.Geo.API.Controllers;
using Unifesspa.UniPlus.Geo.Application.DTOs;
using Unifesspa.UniPlus.Infrastructure.Core.Hateoas;

/// <summary>
/// Builder de <c>_links</c> hypermedia (HATEOAS Level 1, ADR-0029) para
/// <see cref="CepResolvidoDto"/>. Relações: <c>cidade</c>
/// (<c>GET /api/cidades/{codigoIbge}</c>) e <c>estado</c>
/// (<c>GET /api/estados/{uf}</c>) — permitem navegar do CEP para a ficha completa
/// da cidade e do estado resolvidos.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via AddSingleton<IResourceLinksBuilder<CepResolvidoDto>, CepResolvidoLinksBuilder>().")]
internal sealed class CepResolvidoLinksBuilder : IResourceLinksBuilder<CepResolvidoDto>
{
    private static readonly string CidadesControllerNome = GeoLinkPathResolver.ControllerName(nameof(Controllers.CidadesController));
    private static readonly string EstadosControllerNome = GeoLinkPathResolver.ControllerName(nameof(Controllers.EstadosController));

    private readonly LinkGenerator _linkGenerator;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CepResolvidoLinksBuilder(LinkGenerator linkGenerator, IHttpContextAccessor httpContextAccessor)
    {
        ArgumentNullException.ThrowIfNull(linkGenerator);
        ArgumentNullException.ThrowIfNull(httpContextAccessor);
        _linkGenerator = linkGenerator;
        _httpContextAccessor = httpContextAccessor;
    }

    public IReadOnlyDictionary<string, string> Build(CepResolvidoDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        HttpContext? httpContext = _httpContextAccessor.HttpContext;

        string cidade = GeoLinkPathResolver.Resolver(
            _linkGenerator, httpContext, nameof(Controllers.CidadesController.ObterPorCodigoIbge),
            CidadesControllerNome, new { codigoIbge = dto.CodigoIbge });
        string estado = GeoLinkPathResolver.Resolver(
            _linkGenerator, httpContext, nameof(Controllers.EstadosController.ObterPorUf),
            EstadosControllerNome, new { uf = dto.Uf });

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["cidade"] = cidade,
            ["estado"] = estado,
        };
    }
}
