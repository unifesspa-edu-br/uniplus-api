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
/// Vocabulário fechado que o termo de consentimento referencia por código: a forma de aceite.
/// </summary>
/// <remarks>
/// <para>
/// Existe para que o cliente descubra os códigos e os rótulos em runtime, sem manter cópia própria. É somente leitura:
/// muda por versão da API, com a regra que o acompanha, e não por cadastro.
/// </para>
/// <para>
/// A leitura é anônima porque a dos termos de consentimento, que carregam esse campo, já é anônima. O vocabulário não
/// traz dado de pessoa.
/// </para>
/// </remarks>
[ApiController]
[Tags("Vocabularios")]
public sealed class VocabulariosDoTermoConsentimentoController : ControllerBase
{
    private readonly IQueryBus _queryBus;

    public VocabulariosDoTermoConsentimentoController(IQueryBus queryBus)
    {
        _queryBus = queryBus;
    }

    /// <summary>Lista as formas de aceite, com código e rótulo, na ordem de exibição.</summary>
    [HttpGet("vocabularios/formas-aceite")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "codigo-forma-aceite", Versions = [1])]
    [ProducesResponseType(typeof(IReadOnlyList<CodigoComRotulo<FormaAceite>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ListarFormasAceite(CancellationToken cancellationToken)
    {
        IReadOnlyList<CodigoComRotulo<FormaAceite>> formas = await _queryBus
            .Send(new ListarFormasAceiteQuery(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(formas);
    }
}
