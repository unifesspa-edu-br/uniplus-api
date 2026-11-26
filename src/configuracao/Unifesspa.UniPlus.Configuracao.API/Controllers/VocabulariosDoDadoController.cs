namespace Unifesspa.UniPlus.Configuracao.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Application.Queries.Vocabularios;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>Vocabulários dos dados do catálogo.</summary>
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

    /// <summary>Lista as origens dos fatos com código e rótulo.</summary>
    [HttpGet("vocabularios/origens-fato")]
    [EndpointName("ListarOrigensFato")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "origem-fato", Versions = [1])]
    [ProducesResponseType(typeof(IReadOnlyList<CodigoComRotulo<OrigemFato>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ListarOrigensFato(CancellationToken cancellationToken) =>
        Ok(await queryBus.Send(new ListarOrigensFatoQuery(), cancellationToken).ConfigureAwait(false));

    /// <summary>Lista as fontes de valores dos fatos com código e rótulo.</summary>
    [HttpGet("vocabularios/fontes-valores-fato")]
    [EndpointName("ListarFontesValoresFato")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "fonte-valores-fato", Versions = [1])]
    [ProducesResponseType(typeof(IReadOnlyList<CodigoComRotulo<FonteValoresFato>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ListarFontesValoresFato(CancellationToken cancellationToken) =>
        Ok(await queryBus.Send(new ListarFontesValoresFatoQuery(), cancellationToken).ConfigureAwait(false));

    /// <summary>Lista os formatos de texto com código e rótulo.</summary>
    [HttpGet("vocabularios/formatos-texto")]
    [EndpointName("ListarFormatosTexto")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "formato-texto", Versions = [1])]
    [ProducesResponseType(typeof(IReadOnlyList<CodigoComRotulo<FormatoTexto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ListarFormatosTexto(CancellationToken cancellationToken) =>
        Ok(await queryBus.Send(new ListarFormatosTextoQuery(), cancellationToken).ConfigureAwait(false));

    /// <summary>Lista os escopos dos fatos com código e rótulo.</summary>
    [HttpGet("vocabularios/escopos-fato")]
    [EndpointName("ListarEscoposFato")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "escopo-fato", Versions = [1])]
    [ProducesResponseType(typeof(IReadOnlyList<CodigoComRotulo<EscopoFato>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ListarEscoposFato(CancellationToken cancellationToken) =>
        Ok(await queryBus.Send(new ListarEscoposFatoQuery(), cancellationToken).ConfigureAwait(false));

    /// <summary>Lista os componentes de exibição com código e rótulo.</summary>
    [HttpGet("vocabularios/componentes-de-exibicao")]
    [EndpointName("ListarComponentesDeExibicao")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "componente-de-exibicao", Versions = [1])]
    [ProducesResponseType(typeof(IReadOnlyList<CodigoComRotulo<ComponenteDeExibicao>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ListarComponentesDeExibicao(CancellationToken cancellationToken) =>
        Ok(await queryBus.Send(new ListarComponentesDeExibicaoQuery(), cancellationToken).ConfigureAwait(false));

    /// <summary>Lista as hipóteses legais de tratamento com código e rótulo.</summary>
    [HttpGet("vocabularios/hipoteses-legais-tratamento")]
    [EndpointName("ListarHipotesesLegaisTratamento")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "hipotese-legal-tratamento", Versions = [1])]
    [ProducesResponseType(typeof(IReadOnlyList<CodigoComRotulo<HipoteseLegalTratamento>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ListarHipotesesLegaisTratamento(CancellationToken cancellationToken) =>
        Ok(await queryBus.Send(new ListarHipotesesLegaisTratamentoQuery(), cancellationToken).ConfigureAwait(false));
}
