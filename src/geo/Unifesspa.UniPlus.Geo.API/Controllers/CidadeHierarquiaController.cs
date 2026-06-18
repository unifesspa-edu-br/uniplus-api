namespace Unifesspa.UniPlus.Geo.API.Controllers;

using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Geo.Application.DTOs;
using Unifesspa.UniPlus.Geo.Application.Queries.CidadeHierarquia;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Infrastructure.Core.Hateoas;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;

/// <summary>
/// Endpoints públicos de navegação descendente da Cidade: distritos, bairros e
/// autocomplete de logradouros.
/// </summary>
[ApiController]
[Route("api")]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public.")]
public sealed partial class CidadeHierarquiaController : ControllerBase
{
    private const string DistritosResourceTag = "distritos";
    private const string BairrosResourceTag = "bairros";
    private const string LogradourosResourceTag = "logradouros";
    private const int BuscaMaxLength = 256;

    private readonly IQueryBus _queryBus;
    private readonly IResourceLinksBuilder<DistritoDto> _distritoLinks;
    private readonly IResourceLinksBuilder<BairroDto> _bairroLinks;
    private readonly IResourceLinksBuilder<LogradouroResumoDto> _logradouroLinks;

    public CidadeHierarquiaController(
        IQueryBus queryBus,
        IResourceLinksBuilder<DistritoDto> distritoLinks,
        IResourceLinksBuilder<BairroDto> bairroLinks,
        IResourceLinksBuilder<LogradouroResumoDto> logradouroLinks)
    {
        _queryBus = queryBus;
        _distritoLinks = distritoLinks;
        _bairroLinks = bairroLinks;
        _logradouroLinks = logradouroLinks;
    }

    /// <summary>
    /// Lista distritos vigentes de uma Cidade vigente, paginados por cursor opaco.
    /// Código IBGE malformado retorna 400; cidade-pai inexistente retorna 404.
    /// </summary>
    [HttpGet("cidades/{codigoIbge}/distritos")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "distrito", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<DistritoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ListarDistritos(
        string codigoIbge,
        [FromCursor(DistritosResourceTag)] PageRequest page,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);

        if (!CodigoIbgeTemFormatoValido(codigoIbge))
        {
            return CodigoIbgeInvalido();
        }

        ListarDistritosResult resultado = await _queryBus
            .Send(new ListarDistritosQuery(codigoIbge, page.AfterId, page.Limit, page.Direction), cancellationToken)
            .ConfigureAwait(false);

        if (!resultado.CidadeExiste)
        {
            return NotFound();
        }

        DistritoDto[] comLinks = [.. resultado.Items.Select(d => d with { Links = _distritoLinks.Build(d) })];
        return await this.OkPaginatedAsync(
            comLinks, resultado.AnteriorAfterId, resultado.ProximoAfterId, page, DistritosResourceTag,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Lista bairros vigentes de uma Cidade vigente, com busca textual opcional por
    /// <paramref name="q"/> e paginação por cursor opaco.
    /// </summary>
    [HttpGet("cidades/{codigoIbge}/bairros")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "bairro", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<BairroDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ListarBairros(
        string codigoIbge,
        [FromCursor(BairrosResourceTag)] PageRequest page,
        [FromQuery(Name = "q")] string? q,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);

        if (!CodigoIbgeTemFormatoValido(codigoIbge))
        {
            return CodigoIbgeInvalido();
        }

        if (q is { Length: > BuscaMaxLength })
        {
            return TermoBuscaMuitoLongo();
        }

        ListarBairrosResult resultado = await _queryBus
            .Send(new ListarBairrosQuery(codigoIbge, page.AfterId, page.Limit, page.Direction, q), cancellationToken)
            .ConfigureAwait(false);

        if (!resultado.CidadeExiste)
        {
            return NotFound();
        }

        BairroDto[] comLinks = [.. resultado.Items.Select(b => b with { Links = _bairroLinks.Build(b) })];
        return await this.OkPaginatedAsync(
            comLinks, resultado.AnteriorAfterId, resultado.ProximoAfterId, page, BairrosResourceTag,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Lista logradouros vigentes de uma Cidade vigente, com busca textual opcional
    /// por <paramref name="q"/> e paginação por cursor opaco. O uso principal é
    /// autocomplete de endereço quando o usuário não sabe o CEP.
    /// </summary>
    [HttpGet("cidades/{codigoIbge}/logradouros")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "logradouro", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<LogradouroResumoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ListarLogradouros(
        string codigoIbge,
        [FromCursor(LogradourosResourceTag)] PageRequest page,
        [FromQuery(Name = "q")] string? q,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);

        if (!CodigoIbgeTemFormatoValido(codigoIbge))
        {
            return CodigoIbgeInvalido();
        }

        if (q is { Length: > BuscaMaxLength })
        {
            return TermoBuscaMuitoLongo();
        }

        ListarLogradourosResult resultado = await _queryBus
            .Send(new ListarLogradourosQuery(codigoIbge, page.AfterId, page.Limit, page.Direction, q), cancellationToken)
            .ConfigureAwait(false);

        if (!resultado.CidadeExiste)
        {
            return NotFound();
        }

        LogradouroResumoDto[] comLinks = [.. resultado.Items.Select(l => l with { Links = _logradouroLinks.Build(l) })];
        return await this.OkPaginatedAsync(
            comLinks, resultado.AnteriorAfterId, resultado.ProximoAfterId, page, LogradourosResourceTag,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private BadRequestObjectResult CodigoIbgeInvalido() =>
        BadRequest(new ProblemDetails
        {
            Title = "Código IBGE inválido",
            Detail = "O código IBGE deve ter exatamente 7 dígitos.",
            Status = StatusCodes.Status400BadRequest,
        });

    private BadRequestObjectResult TermoBuscaMuitoLongo() =>
        BadRequest(new ProblemDetails
        {
            Title = "Termo de busca muito longo",
            Detail = $"O parâmetro 'q' não pode exceder {BuscaMaxLength} caracteres.",
            Status = StatusCodes.Status400BadRequest,
        });

    private static bool CodigoIbgeTemFormatoValido(string codigoIbge) =>
        !string.IsNullOrWhiteSpace(codigoIbge) && CodigoIbgeRegex().IsMatch(codigoIbge);

    // [0-9] e não \d: em .NET \d casa dígitos Unicode; o código IBGE é ASCII.
    [GeneratedRegex(@"^[0-9]{7}$")]
    private static partial Regex CodigoIbgeRegex();
}
