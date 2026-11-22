namespace Unifesspa.UniPlus.Selecao.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Selecao.Application.Queries.Vocabularios;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>Vocabulário das regras de comprovação documental.</summary>
[ApiController]
[Tags("Vocabularios")]
public sealed class VocabulariosDaComprovacaoController(IQueryBus queryBus) : ControllerBase
{
    /// <summary>Lista as formas de declarar uma regra de comprovação.</summary>
    [HttpGet("vocabularios/formas-comprovacao")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "forma-de-comprovacao", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<CodigoComRotulo<FormaDeComprovacao>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ListarFormasDeComprovacao(CancellationToken cancellationToken)
    {
        IReadOnlyList<CodigoComRotulo<FormaDeComprovacao>> formas = await queryBus
            .Send(new ListarFormasDeComprovacaoQuery(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(formas);
    }
}
