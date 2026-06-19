namespace Unifesspa.UniPlus.Geo.API.Hateoas;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using Unifesspa.UniPlus.Geo.API.Controllers;
using Unifesspa.UniPlus.Geo.Application.DTOs;
using Unifesspa.UniPlus.Infrastructure.Core.Hateoas;

/// <summary>
/// Builder de <c>_links</c> hypermedia (HATEOAS Level 1, ADR-0029) para
/// <see cref="LogradouroProximoDto"/> (item de ranking por distância). Relações
/// <c>cidade</c> (<c>GET /api/cidades/{codigoIbge}</c>) e <c>cep</c>
/// (<c>GET /api/cep/{cep}</c>) — leva ao lookup completo do endereço. Sem <c>self</c>:
/// a API não expõe detalhe de logradouro por Id.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via AddSingleton<IResourceLinksBuilder<LogradouroProximoDto>, LogradouroProximoLinksBuilder>().")]
internal sealed class LogradouroProximoLinksBuilder : IResourceLinksBuilder<LogradouroProximoDto>
{
    private static readonly string CidadesControllerNome = GeoLinkPathResolver.ControllerName(nameof(Controllers.CidadesController));
    private static readonly string CepControllerNome = GeoLinkPathResolver.ControllerName(nameof(Controllers.CepController));

    private readonly LinkGenerator _linkGenerator;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public LogradouroProximoLinksBuilder(LinkGenerator linkGenerator, IHttpContextAccessor httpContextAccessor)
    {
        ArgumentNullException.ThrowIfNull(linkGenerator);
        ArgumentNullException.ThrowIfNull(httpContextAccessor);
        _linkGenerator = linkGenerator;
        _httpContextAccessor = httpContextAccessor;
    }

    public IReadOnlyDictionary<string, string> Build(LogradouroProximoDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        HttpContext? httpContext = _httpContextAccessor.HttpContext;

        string cidade = GeoLinkPathResolver.Resolver(
            _linkGenerator, httpContext, nameof(Controllers.CidadesController.ObterPorCodigoIbge),
            CidadesControllerNome, new { codigoIbge = dto.CidadeCodigoIbge });
        string cep = GeoLinkPathResolver.Resolver(
            _linkGenerator, httpContext, nameof(Controllers.CepController.Resolver),
            CepControllerNome, new { cep = dto.Cep });

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["cidade"] = cidade,
            ["cep"] = cep,
        };
    }
}
