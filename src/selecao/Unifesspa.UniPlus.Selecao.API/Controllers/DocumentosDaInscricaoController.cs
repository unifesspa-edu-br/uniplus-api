namespace Unifesspa.UniPlus.Selecao.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Application.Commands.DocumentosDaInscricao;
using Application.DTOs;
using Application.Queries.DocumentosDaInscricao;
using Application.Services;

using Contracts.Requests;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Os documentos que o candidato envia para atender as exigências da inscrição. Fluxo em 3 passos:
/// iniciar (aqui) → PUT direto ao armazenamento (fora da API) → confirmar (aqui), que põe o arquivo em
/// verificação; o verificador conclui fora da API. A inscrição é a do
/// dono do token, resolvida pelo CPF do claim; um arquivo que não é dele responde 404 como o que não
/// existe. A policy <c>Candidato</c> é declarada em cada action: o host não tem fallback policy.
/// </summary>
[ApiController]
[Route("processos-seletivos/{id:guid}/inscricao/documentos")]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public; sem isso o MVC ignora a classe e nenhum endpoint é registrado.")]
public sealed class DocumentosDaInscricaoController : ControllerBase
{
    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;

    public DocumentosDaInscricaoController(ICommandBus commandBus, IQueryBus queryBus, IDomainErrorMapper mapper)
    {
        _commandBus = commandBus;
        _queryBus = queryBus;
        _mapper = mapper;
    }

    /// <summary>
    /// Passo 1: confere a exigência e devolve a URL pré-assinada de PUT (validade curta), o
    /// <c>Content-Type</c> que o PUT envia, o id do arquivo e o da apresentação. A frente e o verso do
    /// mesmo documento compartilham o <c>apresentacaoId</c>.
    /// </summary>
    [HttpPost("envios")]
    [Authorize(Policy = "Candidato")]
    // TTL alinhado ao da URL pré-assinada devolvida: um replay depois dela expirar devolveria uma URL inutilizável.
    [RequiresIdempotencyKey(TtlSeconds = PrazosDoArquivoEnviado.EnvioSegundos)]
    [ProducesResponseType(typeof(IniciarEnvioDeDocumentoDaInscricaoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> IniciarEnvio(
        Guid id, [FromBody] IniciarEnvioDeDocumentoDaInscricaoRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<IniciarEnvioDeDocumentoDaInscricaoDto> resultado = await _commandBus
            .Send(
                new IniciarEnvioDeDocumentoDaInscricaoCommand(
                    id, request.ExigenciaId, request.OcorrenciaId, request.ApresentacaoId, request.TipoDeMidia, request.TamanhoBytes),
                cancellationToken)
            .ConfigureAwait(false);
        return resultado.IsSuccess ? StatusCode(StatusCodes.Status201Created, resultado.Value) : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Passo 3: o candidato informa o <c>ETag</c> que o PUT devolveu. A API não lê o objeto: grava o arquivo como
    /// em verificação e publica o pedido ao verificador na mesma transação.
    /// </summary>
    [HttpPost("envios/{arquivoId:guid}/confirmacao")]
    [Authorize(Policy = "Candidato")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(typeof(ArquivoDaApresentacaoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConfirmarEnvio(
        Guid id, Guid arquivoId, [FromBody] ConfirmarEnvioDeDocumentoDaInscricaoRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<ArquivoDaApresentacaoDto> resultado = await _commandBus
            .Send(new ConfirmarEnvioDeDocumentoDaInscricaoCommand(id, arquivoId, request.Etag ?? string.Empty), cancellationToken)
            .ConfigureAwait(false);
        return resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Emite a URL pré-assinada de curta validade do arquivo verificado do próprio candidato. Arquivo de outra
    /// pessoa responde 404; o não verificado, 422. A URL é credencial de acesso, então a resposta vai com <c>no-store</c>.
    /// </summary>
    [HttpGet("arquivos/{arquivoId:guid}/acesso")]
    [Authorize(Policy = "Candidato")]
    [VendorMediaType(Resource = "acesso-arquivo-do-candidato", Versions = [1])]
    [ProducesResponseType(typeof(AcessoArquivoDoCandidatoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Acesso(Guid id, Guid arquivoId, CancellationToken cancellationToken)
    {
        Result<AcessoArquivoDoCandidatoDto> resultado = await _queryBus
            .Send(new ObterAcessoDoArquivoDoCandidatoQuery(id, arquivoId), cancellationToken)
            .ConfigureAwait(false);
        if (!resultado.IsSuccess)
        {
            return resultado.ToActionResult(_mapper);
        }

        Response.Headers.CacheControl = "no-store";
        return Ok(resultado.Value);
    }

    /// <summary>
    /// Remove o arquivo do rascunho, apagando o registro e as duas chaves do armazenamento. Depois do
    /// envio da inscrição o arquivo é retido, e a remoção responde 422.
    /// </summary>
    [HttpDelete("arquivos/{arquivoId:guid}")]
    [Authorize(Policy = "Candidato")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RemoverArquivo(Guid id, Guid arquivoId, CancellationToken cancellationToken)
    {
        Result resultado = await _commandBus
            .Send(new RemoverArquivoDaInscricaoCommand(id, arquivoId), cancellationToken)
            .ConfigureAwait(false);
        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }
}
