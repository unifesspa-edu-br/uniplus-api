namespace Unifesspa.UniPlus.Configuracao.API.Controllers;

using System.Collections.Generic;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Application.Queries.Vocabularios;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;

/// <summary>
/// Vocabulários fechados que o calendário de dias não úteis referencia por código: a abrangência do dia não útil.
/// </summary>
/// <remarks>
/// Existe para que o cliente descubra os códigos e os rótulos em runtime, sem manter cópia própria. É somente leitura:
/// muda por versão da API, com a regra que o acompanha, e não por cadastro. A leitura é anônima porque a do calendário já
/// é anônima, e o vocabulário não traz dado de pessoa.
/// </remarks>
[ApiController]
[Tags("Vocabularios")]
public sealed class VocabulariosDoCalendarioController : ControllerBase
{
    private readonly IQueryBus _queryBus;

    public VocabulariosDoCalendarioController(IQueryBus queryBus)
    {
        _queryBus = queryBus;
    }

    /// <summary>Lista as abrangências de dia não útil, com código e rótulo, na ordem de exibição.</summary>
    [HttpGet("vocabularios/abrangencias-dia-nao-util")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "codigo-abrangencia-dia-nao-util", Versions = [1])]
    [ProducesResponseType(typeof(IReadOnlyList<CodigoComRotulo<Abrangencia>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ListarAbrangencias(CancellationToken cancellationToken)
    {
        IReadOnlyList<CodigoComRotulo<Abrangencia>> abrangencias = await _queryBus
            .Send(new ListarAbrangenciasQuery(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(abrangencias);
    }
}
