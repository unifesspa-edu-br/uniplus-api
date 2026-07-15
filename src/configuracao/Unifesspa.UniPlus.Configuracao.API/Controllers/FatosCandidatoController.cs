namespace Unifesspa.UniPlus.Configuracao.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.Queries.FatosCandidato;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;

/// <summary>
/// Endpoints públicos de leitura do catálogo <c>rol_de_fatos_candidato</c> — o
/// vocabulário fechado de fatos do candidato (UNI-REQ-0077, ADR-0111).
/// <strong>Somente leitura</strong>: o catálogo é seed-governado e append-only
/// (adicionar um fato é um PR de desenvolvimento, nunca uma operação de tela), por
/// isso não há rota admin de escrita.
/// </summary>
[ApiController]
[Route("api/configuracao")]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public.")]
public sealed class FatosCandidatoController : ControllerBase
{
    private readonly IQueryBus _queryBus;

    public FatosCandidatoController(IQueryBus queryBus)
    {
        _queryBus = queryBus;
    }

    /// <summary>
    /// Lista o vocabulário completo de fatos do candidato, ordenado por código. É
    /// um conjunto fechado e de baixo volume (nove fatos), portanto não paginado.
    /// </summary>
    [HttpGet("fatos-candidato")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "fato-candidato", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<FatoCandidatoView>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> Listar(CancellationToken cancellationToken)
    {
        ListarFatosCandidatoResult resultado = await _queryBus
            .Send(new ListarFatosCandidatoQuery(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(resultado.Itens);
    }

    /// <summary>
    /// Obtém um fato pela sua chave natural — o código (ex.: <c>COR_RACA</c>).
    /// Retorna 404 quando o código não existe no vocabulário.
    /// </summary>
    [HttpGet("fatos-candidato/{codigo}")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "fato-candidato", Versions = [1])]
    [ProducesResponseType(typeof(FatoCandidatoView), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ObterPorCodigo(string codigo, CancellationToken cancellationToken)
    {
        FatoCandidatoView? fato = await _queryBus
            .Send(new ObterFatoCandidatoPorCodigoQuery(codigo), cancellationToken)
            .ConfigureAwait(false);

        return fato is null ? NotFound() : Ok(fato);
    }
}
