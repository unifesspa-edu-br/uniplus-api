namespace Unifesspa.UniPlus.Geo.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Geo.API.Formatting;
using Unifesspa.UniPlus.Geo.Application.DTOs;
using Unifesspa.UniPlus.Geo.Application.Queries.Cep;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Infrastructure.Core.Hateoas;

/// <summary>
/// Endpoint público de lookup de CEP — reference data (<c>[AllowAnonymous]</c>, sem
/// Idempotency-Key, carga via ETL/F3): <c>GET /api/cep/{cep}</c> resolve o CEP em
/// um endereço estruturado (logradouro/faixa/grande usuário). CEP é dado estável →
/// cache Redis com TTL longo (cache-aside no resolver). Ver ADR-0090.
/// </summary>
[ApiController]
[Route("api")]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public.")]
public sealed class CepController : ControllerBase
{
    private readonly IQueryBus _queryBus;
    private readonly IResourceLinksBuilder<CepResolvidoDto> _linksBuilder;

    public CepController(IQueryBus queryBus, IResourceLinksBuilder<CepResolvidoDto> linksBuilder)
    {
        _queryBus = queryBus;
        _linksBuilder = linksBuilder;
    }

    /// <summary>
    /// Resolve o <paramref name="cep"/> em endereço estruturado. O formato é
    /// decodificado no boundary (ADR-0031): máscara removida e 8 dígitos exigidos —
    /// inválido → 400. Bem-formado mas sem cobertura (logradouro/grande usuário/faixa)
    /// → 404. Resolução positiva traz <c>_links</c> para cidade e estado (ADR-0029).
    /// </summary>
    [HttpGet("cep/{cep}")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "cep", Versions = [1])]
    [ProducesResponseType(typeof(CepResolvidoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> Resolver(string cep, CancellationToken cancellationToken)
    {
        if (!CepValido.TentarNormalizar(cep, out string? cepNormalizado))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "CEP inválido",
                Detail = "O CEP deve ter exatamente 8 dígitos (ex.: 01001000 ou 01001-000).",
                Status = StatusCodes.Status400BadRequest,
            });
        }

        CepResolvidoDto? endereco = await _queryBus
            .Send(new ResolverCepQuery(cepNormalizado), cancellationToken)
            .ConfigureAwait(false);

        if (endereco is null)
        {
            return NotFound();
        }

        return Ok(endereco with { Links = _linksBuilder.Build(endereco) });
    }
}
