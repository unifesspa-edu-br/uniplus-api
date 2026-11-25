namespace Unifesspa.UniPlus.Configuracao.API.Controllers;

using System.Collections.Generic;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Application.Queries.Vocabularios;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// Vocabulário fechado de <c>FormaDoConceito</c>, usado pelo cadastro de tipos de conceito.
/// Somente leitura: o conjunto é governado por código e muda por versão da API, não por cadastro
/// administrativo. Sem paginação, sem <c>_links</c> e sem cache.
/// </summary>
[ApiController]
[Tags("Vocabularios")]
public sealed class VocabulariosDoTipoConceitoController : ControllerBase
{
    private readonly IQueryBus _queryBus;

    public VocabulariosDoTipoConceitoController(IQueryBus queryBus)
    {
        _queryBus = queryBus;
    }

    /// <summary>Lista as naturezas de tipo de conceito, com código e rótulo, na ordem do enum.</summary>
    [HttpGet("vocabularios/naturezas-conceito")]
    [EndpointName("ListarNaturezasDoConceito")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "codigo-natureza-conceito", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<CodigoComRotulo<FormaDoConceito>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ListarNaturezasConceito(CancellationToken cancellationToken)
    {
        IReadOnlyList<CodigoComRotulo<FormaDoConceito>> naturezas = await _queryBus
            .Send(new ListarNaturezasConceitoQuery(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(naturezas);
    }
}
