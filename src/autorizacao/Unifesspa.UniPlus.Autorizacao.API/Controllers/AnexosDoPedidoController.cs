namespace Unifesspa.UniPlus.Autorizacao.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Os anexos do pedido de concessão sob <c>/api/autorizacao/pedidos-de-concessao/{id}/anexos</c>: o envio do PDF por URL
/// assinada, a confirmação que o põe em verificação e a leitura do anexo verificado. No pedido protegido, só o documento
/// declarado na criação se anexa.
/// </summary>
[ApiController]
[Authorize]
public sealed class AnexosDoPedidoController : ControllerBase
{
    private readonly ICommandBus _commandBus;
    private readonly IDomainErrorMapper _mapper;

    public AnexosDoPedidoController(ICommandBus commandBus, IDomainErrorMapper mapper)
    {
        _commandBus = commandBus;
        _mapper = mapper;
    }

    /// <summary>
    /// Pede a URL de PUT assinada de um PDF para anexar ao pedido, só do solicitante e antes da primeira decisão. No pedido
    /// protegido, só o documento declarado na criação, enquanto o pedido o aguarda.
    /// </summary>
    [HttpPost("pedidos-de-concessao/{id:guid}/anexos")]
    // TTL alinhado ao da URL de PUT devolvida: um replay depois de ela expirar devolveria uma URL inutilizável.
    [RequiresIdempotencyKey(TtlSeconds = 15 * 60)]
    [ProducesResponseType(typeof(AnexoParaEnvioDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> EnviarAnexo(
        Guid id, [FromBody] EnviarAnexoDoPedidoRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<AnexoParaEnvioDto> resultado = await _commandBus
            .Send(new CriarAnexoDoPedidoCommand(id, request.TamanhoDoPdf), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess
            ? CreatedAtAction(nameof(LerAnexo), new { id, anexoId = resultado.Value!.Id }, resultado.Value)
            : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// O solicitante informa o <c>ETag</c> que o PUT devolveu. A API não lê o objeto: grava o anexo como em verificação
    /// e publica o pedido ao verificador na mesma transação.
    /// </summary>
    [HttpPost("pedidos-de-concessao/{id:guid}/anexos/{anexoId:guid}/confirmacao")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(typeof(AnexoDoPedidoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ConfirmarAnexo(
        Guid id, Guid anexoId, [FromBody] ConfirmarAnexoDoPedidoRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<AnexoDoPedidoDto> resultado = await _commandBus
            .Send(new ConfirmarAnexoDoPedidoCommand(id, anexoId, request.Etag), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// URL assinada de leitura do anexo, de validade curta, só para o anexo verificado e só para quem vê o pedido. O
    /// acesso entra na trilha.
    /// </summary>
    [HttpGet("pedidos-de-concessao/{id:guid}/anexos/{anexoId:guid}")]
    [VendorMediaType(Resource = "anexo-do-pedido", Versions = [1])]
    [ProducesResponseType(typeof(LeituraDoAnexoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> LerAnexo(Guid id, Guid anexoId, CancellationToken cancellationToken)
    {
        Result<LeituraDoAnexoDto> resultado = await _commandBus
            .Send(new LerAnexoDoPedidoCommand(id, anexoId), cancellationToken)
            .ConfigureAwait(false);
        if (resultado.IsFailure)
        {
            return resultado.ToActionResult(_mapper);
        }

        // A URL é credencial de acesso: nenhum cache a guarda.
        Response.Headers.CacheControl = "no-store";

        return Ok(resultado.Value);
    }
}
