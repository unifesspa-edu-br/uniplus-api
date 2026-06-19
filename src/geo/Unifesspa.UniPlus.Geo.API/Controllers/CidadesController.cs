namespace Unifesspa.UniPlus.Geo.API.Controllers;

using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Geo.Application.DTOs;
using Unifesspa.UniPlus.Geo.Application.Queries.Cidades;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Infrastructure.Core.Hateoas;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;

/// <summary>
/// Endpoints públicos de leitura de Cidades — reference data
/// (<c>[AllowAnonymous]</c>, sem Idempotency-Key, carga via ETL/F3):
/// <c>GET /api/cidades?uf=&amp;q=</c> (lista paginada com filtro por UF e busca
/// textual) e <c>GET /api/cidades/{codigoIbge}</c> (detalhe pela chave natural).
/// </summary>
[ApiController]
[Route("api")]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public.")]
public sealed partial class CidadesController : ControllerBase
{
    private const string ResourceTag = "cidades";

    /// <summary>Limite defensivo de comprimento do termo de busca (cobre o Nome máximo + margem).</summary>
    private const int BuscaMaxLength = 256;

    private readonly IQueryBus _queryBus;
    private readonly IResourceLinksBuilder<CidadeResumoDto> _resumoLinks;
    private readonly IResourceLinksBuilder<CidadeDetalheDto> _detalheLinks;

    public CidadesController(
        IQueryBus queryBus,
        IResourceLinksBuilder<CidadeResumoDto> resumoLinks,
        IResourceLinksBuilder<CidadeDetalheDto> detalheLinks)
    {
        _queryBus = queryBus;
        _resumoLinks = resumoLinks;
        _detalheLinks = detalheLinks;
    }

    /// <summary>
    /// Lista as Cidades vigentes, paginadas por cursor opaco bidirecional (ADR-0026 +
    /// ADR-0089), com filtros opcionais: <c>uf</c> (filtro por UF) e <c>q</c> (busca
    /// textual acento/caixa-insensível sobre o nome). Os filtros viajam como query
    /// params e combinam com o cursor — o cliente reanexa-os a cada página ao seguir
    /// o <c>cursor</c> do header <c>Link</c>.
    /// </summary>
    [HttpGet("cidades")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "cidade", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<CidadeResumoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Listar(
        [FromCursor(ResourceTag, RequireSortKey = true)] PageRequest page,
        [FromQuery(Name = "uf")] string? uf,
        [FromQuery(Name = "q")] string? q,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);

        if (q is { Length: > BuscaMaxLength })
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Termo de busca muito longo",
                Detail = $"O parâmetro 'q' não pode exceder {BuscaMaxLength} caracteres.",
                Status = StatusCodes.Status400BadRequest,
            });
        }

        ListarCidadesResult resultado = await _queryBus
            .Send(new ListarCidadesQuery(page.AfterSortKey, page.AfterId, page.Limit, page.Direction, uf, q), cancellationToken)
            .ConfigureAwait(false);

        // HATEOAS Level 1 (ADR-0029 §"Coleção"): cada item carrega seu _links.self.
        CidadeResumoDto[] comLinks = [.. resultado.Items.Select(c => c with { Links = _resumoLinks.Build(c) })];

        return await this.OkPaginatedOrdenadoAsync(
            comLinks, resultado.Anterior, resultado.Proximo, page, ResourceTag,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Obtém uma Cidade pela chave natural <paramref name="codigoIbge"/> (7 dígitos),
    /// com territorial embutido + indicador 1:1. Formato inválido → 400 (decode no
    /// boundary, ADR-0031); bem-formado e inexistente → 404.
    /// </summary>
    [HttpGet("cidades/{codigoIbge}")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "cidade", Versions = [1])]
    [ProducesResponseType(typeof(CidadeDetalheDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ObterPorCodigoIbge(string codigoIbge, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(codigoIbge) || !CodigoIbgeRegex().IsMatch(codigoIbge))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Código IBGE inválido",
                Detail = "O código IBGE deve ter exatamente 7 dígitos.",
                Status = StatusCodes.Status400BadRequest,
            });
        }

        CidadeDetalheDto? cidade = await _queryBus
            .Send(new ObterCidadePorCodigoIbgeQuery(codigoIbge), cancellationToken)
            .ConfigureAwait(false);

        if (cidade is null)
        {
            return NotFound();
        }

        return Ok(cidade with { Links = _detalheLinks.Build(cidade) });
    }

    // [0-9] e não \d: em .NET \d casa dígitos Unicode (ex.: árabe-índicos); a chave
    // IBGE é ASCII — um "7 dígitos" não-ASCII deve ser 400 (formato), não 404.
    [GeneratedRegex(@"^[0-9]{7}$")]
    private static partial Regex CodigoIbgeRegex();
}
