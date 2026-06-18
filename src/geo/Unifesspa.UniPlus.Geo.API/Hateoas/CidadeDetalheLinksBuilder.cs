namespace Unifesspa.UniPlus.Geo.API.Hateoas;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using Unifesspa.UniPlus.Geo.API.Controllers;
using Unifesspa.UniPlus.Geo.Application.DTOs;
using Unifesspa.UniPlus.Infrastructure.Core.Hateoas;

/// <summary>
/// Builder de <c>_links</c> hypermedia (HATEOAS Level 1, ADR-0029) para
/// <see cref="CidadeDetalheDto"/>. Relações: <c>self</c>
/// (<c>GET /api/cidades/{codigoIbge}</c>), <c>estado</c>
/// (<c>GET /api/estados/{uf}</c>), <c>collection</c> (<c>GET /api/cidades</c>) e
/// coleções descendentes da cidade.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via AddSingleton<IResourceLinksBuilder<CidadeDetalheDto>, CidadeDetalheLinksBuilder>().")]
internal sealed class CidadeDetalheLinksBuilder : IResourceLinksBuilder<CidadeDetalheDto>
{
    private static readonly string CidadesControllerNome = GeoLinkPathResolver.ControllerName(nameof(Controllers.CidadesController));
    private static readonly string HierarquiaControllerNome = GeoLinkPathResolver.ControllerName(nameof(CidadeHierarquiaController));
    private static readonly string EstadosControllerNome = GeoLinkPathResolver.ControllerName(nameof(Controllers.EstadosController));

    private readonly LinkGenerator _linkGenerator;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CidadeDetalheLinksBuilder(LinkGenerator linkGenerator, IHttpContextAccessor httpContextAccessor)
    {
        ArgumentNullException.ThrowIfNull(linkGenerator);
        ArgumentNullException.ThrowIfNull(httpContextAccessor);
        _linkGenerator = linkGenerator;
        _httpContextAccessor = httpContextAccessor;
    }

    public IReadOnlyDictionary<string, string> Build(CidadeDetalheDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        HttpContext? httpContext = _httpContextAccessor.HttpContext;

        string self = GeoLinkPathResolver.Resolver(
            _linkGenerator, httpContext, nameof(Controllers.CidadesController.ObterPorCodigoIbge),
            CidadesControllerNome, new { codigoIbge = dto.CodigoIbge });
        string estado = GeoLinkPathResolver.Resolver(
            _linkGenerator, httpContext, nameof(Controllers.EstadosController.ObterPorUf),
            EstadosControllerNome, new { uf = dto.Uf });
        string collection = GeoLinkPathResolver.Resolver(
            _linkGenerator, httpContext, nameof(Controllers.CidadesController.Listar),
            CidadesControllerNome, values: null);
        string distritos = GeoLinkPathResolver.Resolver(
            _linkGenerator, httpContext, nameof(CidadeHierarquiaController.ListarDistritos),
            HierarquiaControllerNome, new { codigoIbge = dto.CodigoIbge });
        string bairros = GeoLinkPathResolver.Resolver(
            _linkGenerator, httpContext, nameof(CidadeHierarquiaController.ListarBairros),
            HierarquiaControllerNome, new { codigoIbge = dto.CodigoIbge });
        string logradouros = GeoLinkPathResolver.Resolver(
            _linkGenerator, httpContext, nameof(CidadeHierarquiaController.ListarLogradouros),
            HierarquiaControllerNome, new { codigoIbge = dto.CodigoIbge });

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["self"] = self,
            ["estado"] = estado,
            ["collection"] = collection,
            ["distritos"] = distritos,
            ["bairros"] = bairros,
            ["logradouros"] = logradouros,
        };
    }
}
