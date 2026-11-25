namespace Unifesspa.UniPlus.Configuracao.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Application.Queries.Vocabularios;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>Vocabulários do tipo e da quantidade de valores de um dado.</summary>
[ApiController]
[Tags("Vocabularios")]
public sealed class VocabulariosDoDadoController(IQueryBus queryBus) : ControllerBase
{
    /// <summary>Lista os tipos de dado com código e rótulo.</summary>
    [HttpGet("vocabularios/tipos-do-dado")]
    [EndpointName("ListarTiposDoDado")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "tipo-do-dado", Versions = [1])]
    [ProducesResponseType(typeof(IReadOnlyList<CodigoComRotulo<TipoDoDado>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ListarTiposDoDado(CancellationToken cancellationToken) =>
        Ok(await queryBus.Send(new ListarTiposDoDadoQuery(), cancellationToken).ConfigureAwait(false));

    /// <summary>Lista as quantidades de valores com código e rótulo.</summary>
    [HttpGet("vocabularios/quantidades-de-valores")]
    [EndpointName("ListarQuantidadesDeValores")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "quantidade-de-valores", Versions = [1])]
    [ProducesResponseType(typeof(IReadOnlyList<CodigoComRotulo<QuantidadeDeValores>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ListarQuantidadesDeValores(CancellationToken cancellationToken) =>
        Ok(await queryBus.Send(new ListarQuantidadesDeValoresQuery(), cancellationToken).ConfigureAwait(false));
}
