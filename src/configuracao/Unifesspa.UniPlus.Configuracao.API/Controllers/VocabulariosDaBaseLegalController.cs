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

/// <summary>
/// Vocabulário fechado de <c>TipoInstrumentoNormativo</c>, usado pela Base Legal de Bônus Regional.
/// Somente leitura: o conjunto é governado por código e muda por versão da API, não por cadastro
/// administrativo. Sem paginação, sem <c>_links</c> e sem cache.
/// </summary>
[ApiController]
[Tags("Vocabularios")]
public sealed class VocabulariosDaBaseLegalController : ControllerBase
{
    private readonly IQueryBus _queryBus;

    public VocabulariosDaBaseLegalController(IQueryBus queryBus)
    {
        _queryBus = queryBus;
    }

    /// <summary>Lista os tipos de instrumento normativo da Base Legal de Bônus Regional, com código, rótulo e descrição.</summary>
    [HttpGet("vocabularios/tipos-instrumento-normativo")]
    [EndpointName("ListarTiposDeInstrumentoNormativo")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "codigo-tipo-instrumento-normativo", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<CodigoComDescricao<TipoInstrumentoNormativo>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ListarCodigosTipoInstrumentoNormativo(CancellationToken cancellationToken)
    {
        IReadOnlyList<CodigoComDescricao<TipoInstrumentoNormativo>> codigos = await _queryBus
            .Send(new ListarCodigosTipoInstrumentoNormativoQuery(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(codigos);
    }
}
