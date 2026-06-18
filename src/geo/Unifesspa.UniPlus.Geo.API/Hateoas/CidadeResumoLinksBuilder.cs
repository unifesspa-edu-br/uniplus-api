namespace Unifesspa.UniPlus.Geo.API.Hateoas;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using Unifesspa.UniPlus.Geo.API.Controllers;
using Unifesspa.UniPlus.Geo.Application.DTOs;
using Unifesspa.UniPlus.Infrastructure.Core.Hateoas;

/// <summary>
/// Builder de <c>_links</c> hypermedia (HATEOAS Level 1, ADR-0029) para
/// <see cref="CidadeResumoDto"/> (item de coleção). Relação <c>self</c>
/// (<c>GET /api/cidades/{codigoIbge}</c>) — leva ao detalhe completo da cidade.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via AddSingleton<IResourceLinksBuilder<CidadeResumoDto>, CidadeResumoLinksBuilder>().")]
internal sealed class CidadeResumoLinksBuilder : IResourceLinksBuilder<CidadeResumoDto>
{
    private static readonly string CidadesControllerNome = GeoLinkPathResolver.ControllerName(nameof(Controllers.CidadesController));

    private readonly LinkGenerator _linkGenerator;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CidadeResumoLinksBuilder(LinkGenerator linkGenerator, IHttpContextAccessor httpContextAccessor)
    {
        ArgumentNullException.ThrowIfNull(linkGenerator);
        ArgumentNullException.ThrowIfNull(httpContextAccessor);
        _linkGenerator = linkGenerator;
        _httpContextAccessor = httpContextAccessor;
    }

    public IReadOnlyDictionary<string, string> Build(CidadeResumoDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        HttpContext? httpContext = _httpContextAccessor.HttpContext;

        string self = GeoLinkPathResolver.Resolver(
            _linkGenerator, httpContext, nameof(Controllers.CidadesController.ObterPorCodigoIbge),
            CidadesControllerNome, new { codigoIbge = dto.CodigoIbge });

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["self"] = self,
        };
    }
}
