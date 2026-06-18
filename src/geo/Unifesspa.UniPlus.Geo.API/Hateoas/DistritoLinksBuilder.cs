namespace Unifesspa.UniPlus.Geo.API.Hateoas;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using Unifesspa.UniPlus.Geo.API.Controllers;
using Unifesspa.UniPlus.Geo.Application.DTOs;
using Unifesspa.UniPlus.Infrastructure.Core.Hateoas;

/// <summary>
/// Builder de <c>_links</c> para itens de distrito. Não emite <c>self</c>, pois a
/// API ainda não expõe detalhe de distrito por Id.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via AddSingleton<IResourceLinksBuilder<DistritoDto>, DistritoLinksBuilder>().")]
internal sealed class DistritoLinksBuilder : IResourceLinksBuilder<DistritoDto>
{
    private static readonly string CidadesControllerNome = GeoLinkPathResolver.ControllerName(nameof(Controllers.CidadesController));
    private static readonly string HierarquiaControllerNome = GeoLinkPathResolver.ControllerName(nameof(CidadeHierarquiaController));

    private readonly LinkGenerator _linkGenerator;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public DistritoLinksBuilder(LinkGenerator linkGenerator, IHttpContextAccessor httpContextAccessor)
    {
        ArgumentNullException.ThrowIfNull(linkGenerator);
        ArgumentNullException.ThrowIfNull(httpContextAccessor);
        _linkGenerator = linkGenerator;
        _httpContextAccessor = httpContextAccessor;
    }

    public IReadOnlyDictionary<string, string> Build(DistritoDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        HttpContext? httpContext = _httpContextAccessor.HttpContext;

        string cidade = GeoLinkPathResolver.Resolver(
            _linkGenerator, httpContext, nameof(Controllers.CidadesController.ObterPorCodigoIbge),
            CidadesControllerNome, new { codigoIbge = dto.CidadeCodigoIbge });
        string collection = GeoLinkPathResolver.Resolver(
            _linkGenerator, httpContext, nameof(CidadeHierarquiaController.ListarDistritos),
            HierarquiaControllerNome, new { codigoIbge = dto.CidadeCodigoIbge });

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["cidade"] = cidade,
            ["collection"] = collection,
        };
    }
}
