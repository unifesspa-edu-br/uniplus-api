namespace Unifesspa.UniPlus.Configuracao.API.Controllers;

using System.Collections.Generic;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Queries.Vocabularios;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;

/// <summary>
/// Vocabulário fechado de <c>FaseCanonica</c> (UNI-REQ-0139): as fases do conjunto canônico.
/// Somente leitura: o conjunto é governado por código e muda por versão da API, não por cadastro
/// administrativo. Sem paginação, sem <c>_links</c> e sem cache.
/// </summary>
[ApiController]
[Tags("Vocabularios")]
public sealed class VocabulariosDaFaseCanonicaController : ControllerBase
{
    private readonly IQueryBus _queryBus;

    public VocabulariosDaFaseCanonicaController(IQueryBus queryBus)
    {
        _queryBus = queryBus;
    }

    /// <summary>Lista as dezesseis fases do conjunto canônico, com código e rótulo.</summary>
    [HttpGet("vocabularios/fases-canonicas")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "codigo-fase-canonica", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<FaseCanonicaVocabularioDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ListarCodigosFaseCanonica(CancellationToken cancellationToken)
    {
        IReadOnlyList<FaseCanonicaVocabularioDto> codigos = await _queryBus
            .Send(new ListarCodigosFaseCanonicaQuery(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(codigos);
    }
}
