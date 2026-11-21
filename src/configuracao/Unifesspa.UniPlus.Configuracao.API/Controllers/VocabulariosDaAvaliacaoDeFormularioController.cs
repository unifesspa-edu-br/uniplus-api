namespace Unifesspa.UniPlus.Configuracao.API.Controllers;

using System.Collections.Generic;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Application.Queries.Vocabularios;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// Vocabulários fechados do resultado de uma regra (<c>Ternario</c>) e do estado de um fato (<c>EstadoFato</c>) que a
/// avaliação de um formulário devolve. Somente leitura: o conjunto é governado por código e muda por versão da API, não
/// por cadastro administrativo. Sem paginação, sem <c>_links</c> e sem cache.
/// </summary>
[ApiController]
[Tags("Vocabularios")]
public sealed class VocabulariosDaAvaliacaoDeFormularioController : ControllerBase
{
    private readonly IQueryBus _queryBus;

    public VocabulariosDaAvaliacaoDeFormularioController(IQueryBus queryBus)
    {
        _queryBus = queryBus;
    }

    /// <summary>Lista os resultados ternários de uma regra avaliada, com código e rótulo, na ordem do enum.</summary>
    [HttpGet("vocabularios/ternarios")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "codigo-ternario", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<CodigoComRotulo<Ternario>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ListarTernarios(CancellationToken cancellationToken)
    {
        IReadOnlyList<CodigoComRotulo<Ternario>> ternarios = await _queryBus
            .Send(new ListarTernariosQuery(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(ternarios);
    }

    /// <summary>Lista os estados de resolução de um fato do candidato, com código e rótulo, na ordem do enum.</summary>
    [HttpGet("vocabularios/estados-fato")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "codigo-estado-fato", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<CodigoComRotulo<EstadoFato>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ListarEstadosFato(CancellationToken cancellationToken)
    {
        IReadOnlyList<CodigoComRotulo<EstadoFato>> estados = await _queryBus
            .Send(new ListarEstadosFatoQuery(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(estados);
    }
}
