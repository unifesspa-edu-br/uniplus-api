namespace Unifesspa.UniPlus.Selecao.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Kernel.Results;
using Application.Commands.DocumentosEdital;
using Application.DTOs;

/// <summary>
/// Upload direto do documento (PDF) do Edital via URL pre-assinada do MinIO
/// (Story #759, T3 #784) — o arquivo nunca trafega pela API. Fluxo em 3
/// passos: iniciar (aqui) → PUT direto ao MinIO (fora da API) → confirmar
/// (aqui). O <c>hash_edital</c> do snapshot de publicação (T4 #785) é lido do
/// registro confirmado.
/// </summary>
[ApiController]
[Route("api/selecao/processos-seletivos/{processoSeletivoId:guid}/documentos-edital")]
// Mesma justificativa de ProcessoSeletivoController: fluxo administrativo de
// publicação, sem fallback policy — restrito a plataforma-admin.
[Authorize(Roles = "plataforma-admin")]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public; sem isso o MVC ignora a classe e nenhum endpoint é registrado.")]
public sealed class DocumentosEditalController : ControllerBase
{
    private readonly ICommandBus _commandBus;
    private readonly IDomainErrorMapper _mapper;

    public DocumentosEditalController(ICommandBus commandBus, IDomainErrorMapper mapper)
    {
        _commandBus = commandBus;
        _mapper = mapper;
    }

    /// <summary>
    /// Passo 1: cria o registro pendente vinculado ao processo e devolve a
    /// URL pre-assinada de PUT (TTL curto) + o id do documento.
    /// </summary>
    [HttpPost]
    // TTL alinhado ao da URL pre-assinada devolvida (não ao teto default de
    // 24h, ADR-0027) — sem isso, um replay depois da URL expirar devolveria
    // uma URL inutilizável ao cliente. Ver XML doc de TtlUploadSegundos.
    [RequiresIdempotencyKey(TtlSeconds = IniciarUploadDocumentoEditalCommandHandler.TtlUploadSegundos)]
    [ProducesResponseType(typeof(IniciarUploadDocumentoEditalDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> IniciarUpload(Guid processoSeletivoId, CancellationToken cancellationToken)
    {
        Result<IniciarUploadDocumentoEditalDto> resultado = await _commandBus.Send(
            new IniciarUploadDocumentoEditalCommand(processoSeletivoId), cancellationToken);
        if (resultado.IsSuccess)
            return StatusCode(StatusCodes.Status201Created, resultado.Value);
        return resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Passo 3: lê o objeto do MinIO, valida content-type/tamanho/assinatura
    /// de arquivo, calcula o sha256 server-side e finaliza o documento como
    /// imutável.
    /// </summary>
    [HttpPost("{documentoEditalId:guid}/confirmacao")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(typeof(DocumentoEditalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ConfirmarUpload(
        Guid processoSeletivoId, Guid documentoEditalId, CancellationToken cancellationToken)
    {
        Result<DocumentoEditalDto> resultado = await _commandBus.Send(
            new ConfirmarUploadDocumentoEditalCommand(processoSeletivoId, documentoEditalId), cancellationToken);
        if (resultado.IsSuccess)
            return Ok(resultado.Value);
        return resultado.ToActionResult(_mapper);
    }
}
