namespace Unifesspa.UniPlus.Geo.API.Controllers;

using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Geo.Application.DTOs;
using Unifesspa.UniPlus.Geo.Application.Queries.Estados;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Infrastructure.Core.Hateoas;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;

/// <summary>
/// Endpoints públicos de leitura de Estados (UFs) — reference data
/// (<c>[AllowAnonymous]</c>, sem Idempotency-Key, carga via ETL/F3):
/// <c>GET /api/estados</c> (lista paginada por cursor) e
/// <c>GET /api/estados/{uf}</c> (detalhe pela chave natural <c>uf</c>).
/// </summary>
[ApiController]
[Route("api")]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public.")]
public sealed partial class EstadosController : ControllerBase
{
    private const string ResourceTag = "estados";

    private readonly IQueryBus _queryBus;
    private readonly IResourceLinksBuilder<EstadoDto> _linksBuilder;

    public EstadosController(IQueryBus queryBus, IResourceLinksBuilder<EstadoDto> linksBuilder)
    {
        _queryBus = queryBus;
        _linksBuilder = linksBuilder;
    }

    /// <summary>
    /// Lista as UFs vigentes, paginadas por cursor opaco bidirecional (ADR-0026 +
    /// ADR-0089). Navegação via header <c>Link</c> (RFC 5988/8288); cada item
    /// carrega seu <c>_links.self</c> resolvível para <c>GET /api/estados/{uf}</c>.
    /// </summary>
    [HttpGet("estados")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "estado", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<EstadoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Listar(
        [FromCursor(ResourceTag)] PageRequest page,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);

        ListarEstadosResult resultado = await _queryBus
            .Send(new ListarEstadosQuery(page.AfterSortKey, page.AfterId, page.Limit, page.Direction), cancellationToken)
            .ConfigureAwait(false);

        // HATEOAS Level 1 (ADR-0029 §"Coleção"): cada item carrega seu _links.self.
        EstadoDto[] comLinks = [.. resultado.Items.Select(e => e with { Links = _linksBuilder.Build(e) })];

        return await this.OkPaginatedOrdenadoAsync(
            comLinks, resultado.Anterior, resultado.Proximo, page, ResourceTag,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Obtém um Estado pela chave natural <paramref name="uf"/> (2 letras). Formato
    /// inválido → 400 (decode no boundary, ADR-0031); bem-formado e inexistente → 404.
    /// </summary>
    [HttpGet("estados/{uf}")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "estado", Versions = [1])]
    [ProducesResponseType(typeof(EstadoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ObterPorUf(string uf, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(uf) || !UfRegex().IsMatch(uf))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "UF inválida",
                Detail = "A UF deve ter exatamente 2 letras (ex.: PA).",
                Status = StatusCodes.Status400BadRequest,
            });
        }

        EstadoDto? estado = await _queryBus
            .Send(new ObterEstadoPorUfQuery(uf), cancellationToken)
            .ConfigureAwait(false);

        if (estado is null)
        {
            return NotFound();
        }

        return Ok(estado with { Links = _linksBuilder.Build(estado) });
    }

    [GeneratedRegex(@"^[A-Za-z]{2}$")]
    private static partial Regex UfRegex();
}
