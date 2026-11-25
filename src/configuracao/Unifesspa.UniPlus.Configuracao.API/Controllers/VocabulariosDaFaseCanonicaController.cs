namespace Unifesspa.UniPlus.Configuracao.API.Controllers;

using System.Collections.Generic;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Queries.Vocabularios;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;

/// <summary>
/// Vocabulários fechados de <c>FaseCanonica</c>: as fases do conjunto canônico (UNI-REQ-0139) e as origens da data da fase.
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

    /// <summary>Lista as quatorze fases do conjunto canônico, com código e rótulo.</summary>
    [HttpGet("vocabularios/fases-canonicas")]
    [EndpointName("ListarCodigosDeTipoDeFase")]
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

    /// <summary>Lista as origens da data de uma fase, com código e rótulo, na ordem de exibição.</summary>
    [HttpGet("vocabularios/origens-data-fase")]
    [EndpointName("ListarOrigensDaDataDaFase")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "codigo-origem-data-fase", Versions = [1])]
    [ProducesResponseType(typeof(IReadOnlyList<CodigoComRotulo<OrigemDataFase>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ListarOrigensDataFase(CancellationToken cancellationToken)
    {
        IReadOnlyList<CodigoComRotulo<OrigemDataFase>> origens = await _queryBus
            .Send(new ListarOrigensDataFaseQuery(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(origens);
    }
}
