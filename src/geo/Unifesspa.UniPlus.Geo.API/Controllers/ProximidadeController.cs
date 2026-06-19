namespace Unifesspa.UniPlus.Geo.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Geo.API.Formatting;
using Unifesspa.UniPlus.Geo.Application.DTOs;
using Unifesspa.UniPlus.Geo.Application.Queries.Proximidade;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Infrastructure.Core.Hateoas;

/// <summary>
/// Consultas geoespaciais de proximidade (ADR-0091) — reference data
/// (<c>[AllowAnonymous]</c>, sem Idempotency-Key): dado um ponto (lat/long) e um raio,
/// devolve as cidades (<c>GET /api/cidades/proximas</c>) ou logradouros
/// (<c>GET /api/logradouros/proximos</c>) dentro do raio, ordenados por distância
/// crescente (top-N). Filtro por <c>ST_DWithin</c> (índice GIST) + ordenação por
/// <c>ST_Distance</c>. Ranking por distância — sem cursor/paginação (ADR-0026/0089
/// não se aplicam).
/// </summary>
[ApiController]
[Route("api")]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public.")]
public sealed class ProximidadeController : ControllerBase
{
    private readonly IQueryBus _queryBus;
    private readonly IResourceLinksBuilder<CidadeProximaDto> _cidadeLinks;
    private readonly IResourceLinksBuilder<LogradouroProximoDto> _logradouroLinks;
    private readonly GeoProximidadeOptions _opcoes;

    public ProximidadeController(
        IQueryBus queryBus,
        IResourceLinksBuilder<CidadeProximaDto> cidadeLinks,
        IResourceLinksBuilder<LogradouroProximoDto> logradouroLinks,
        IOptions<GeoProximidadeOptions> opcoes)
    {
        ArgumentNullException.ThrowIfNull(opcoes);
        _queryBus = queryBus;
        _cidadeLinks = cidadeLinks;
        _logradouroLinks = logradouroLinks;
        _opcoes = opcoes.Value;
    }

    /// <summary>
    /// Cidades vigentes cuja coordenada está a até <c>raioKm</c> do ponto
    /// (<c>lat</c>, <c>long</c> em graus decimais, <c>InvariantCulture</c>), ordenadas
    /// por distância crescente. Parâmetro ausente ou fora de faixa → 400 (ADR-0031).
    /// </summary>
    [HttpGet("cidades/proximas")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "cidade-proxima", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<CidadeProximaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> CidadesProximas(
        [FromQuery] double? lat,
        [FromQuery(Name = "long")] double? longitude,
        [FromQuery] double? raioKm,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        if (!ConsultaProximidade.TentarCriar(
                lat, longitude, raioKm, limit, _opcoes, out ConsultaProximidade? consulta, out ProblemDetails? erro))
        {
            return BadRequest(erro);
        }

        IReadOnlyList<CidadeProximaDto> itens = await _queryBus
            .Send(new BuscarCidadesProximasQuery(consulta.Latitude, consulta.Longitude, consulta.RaioKm, consulta.Limit), cancellationToken)
            .ConfigureAwait(false);

        // HATEOAS Level 1 (ADR-0029): cada item leva _links para a cidade canônica.
        CidadeProximaDto[] comLinks = [.. itens.Select(c => c with { Links = _cidadeLinks.Build(c) })];
        return Ok(comLinks);
    }

    /// <summary>
    /// Logradouros vigentes (em cidade vigente) cuja coordenada está a até <c>raioKm</c>
    /// do ponto, ordenados por distância crescente. Útil para "qual a rua/CEP mais
    /// próximo deste ponto". Parâmetro ausente ou fora de faixa → 400 (ADR-0031).
    /// </summary>
    [HttpGet("logradouros/proximos")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "logradouro-proximo", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<LogradouroProximoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> LogradourosProximos(
        [FromQuery] double? lat,
        [FromQuery(Name = "long")] double? longitude,
        [FromQuery] double? raioKm,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        if (!ConsultaProximidade.TentarCriar(
                lat, longitude, raioKm, limit, _opcoes, out ConsultaProximidade? consulta, out ProblemDetails? erro))
        {
            return BadRequest(erro);
        }

        IReadOnlyList<LogradouroProximoDto> itens = await _queryBus
            .Send(new BuscarLogradourosProximosQuery(consulta.Latitude, consulta.Longitude, consulta.RaioKm, consulta.Limit), cancellationToken)
            .ConfigureAwait(false);

        LogradouroProximoDto[] comLinks = [.. itens.Select(l => l with { Links = _logradouroLinks.Build(l) })];
        return Ok(comLinks);
    }
}
