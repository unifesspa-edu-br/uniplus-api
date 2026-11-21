namespace Unifesspa.UniPlus.Configuracao.API.Controllers;

using System.Collections.Generic;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Queries.Vocabularios;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;

/// <summary>
/// Vocabulário fechado de <c>TipoBanca</c> (UNI-REQ-0139): os tipos de banca do conjunto canônico.
/// Somente leitura: o conjunto é governado por código e muda por versão da API, não por cadastro
/// administrativo. Sem paginação, sem <c>_links</c> e sem cache.
/// </summary>
[ApiController]
[Tags("Vocabularios")]
public sealed class VocabulariosDaBancaController : ControllerBase
{
    private readonly IQueryBus _queryBus;

    public VocabulariosDaBancaController(IQueryBus queryBus)
    {
        _queryBus = queryBus;
    }

    /// <summary>Lista os seis tipos de banca do conjunto canônico, com código e rótulo.</summary>
    [HttpGet("vocabularios/tipos-banca")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "codigo-tipo-banca", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<TipoBancaVocabularioDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ListarCodigosTipoBanca(CancellationToken cancellationToken)
    {
        IReadOnlyList<TipoBancaVocabularioDto> codigos = await _queryBus
            .Send(new ListarCodigosTipoBancaQuery(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(codigos);
    }
}
