namespace Unifesspa.UniPlus.Selecao.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Application.Commands.DocumentosAPublicar;
using Application.DTOs;
using Application.Queries.DocumentosAPublicar;
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
/// Envio direto do documento que o processo vai publicar (PDF, DOCX ou ODT) por URL pré-assinada do
/// armazenamento privado — o arquivo nunca trafega pela API. Fluxo em 3 passos: iniciar (aqui) → PUT
/// direto ao armazenamento (fora da API) → confirmar (aqui). O documento confirmado é o que o edital,
/// o anexo ou o ato referencia.
/// </summary>
[ApiController]
[Route("processos-seletivos/{processoSeletivoId:guid}/documentos-a-publicar")]
// Mesma justificativa de ProcessoSeletivoController: fluxo administrativo de
// publicação, sem fallback policy — restrito a plataforma-admin.
[Authorize(Roles = "plataforma-admin")]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public; sem isso o MVC ignora a classe e nenhum endpoint é registrado.")]
public sealed class DocumentosAPublicarController : ControllerBase
{
    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;

    public DocumentosAPublicarController(ICommandBus commandBus, IQueryBus queryBus, IDomainErrorMapper mapper)
    {
        _commandBus = commandBus;
        _queryBus = queryBus;
        _mapper = mapper;
    }

    /// <summary>
    /// Lista os documentos do processo, do mais recente para o mais antigo, com
    /// o pendente e o confirmado lado a lado — é por aqui que o editor retoma um
    /// rascunho depois de um refresh, em vez de reenviar o arquivo que já subiu.
    /// Devolve apenas metadados: nenhum endereço de storage e nenhuma URL
    /// assinada atravessa o contrato.
    /// </summary>
    [HttpGet]
    [VendorMediaType(Resource = "documento-a-publicar", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<DocumentoAPublicarDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> Listar(Guid processoSeletivoId, CancellationToken cancellationToken)
    {
        Result<ListarDocumentosAPublicarResult> resultado = await _queryBus
            .Send(new ListarDocumentosAPublicarQuery(processoSeletivoId), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? Ok(resultado.Value!.Items) : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Passo 1: cria o registro pendente com o nome e o tipo de mídia e devolve a URL pré-assinada
    /// de PUT (validade curta), o <c>Content-Type</c> que o PUT envia e o id do documento.
    /// </summary>
    [HttpPost]
    // TTL alinhado ao da URL pré-assinada devolvida (não ao teto default de 24h, ADR-0027) — sem
    // isso, um replay depois da URL expirar devolveria uma URL inutilizável ao cliente.
    [RequiresIdempotencyKey(TtlSeconds = PrazosDoArquivoEnviado.EnvioSegundos)]
    [ProducesResponseType(typeof(IniciarEnvioDoDocumentoAPublicarDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> IniciarEnvio(
        Guid processoSeletivoId, [FromBody] IniciarEnvioDoDocumentoAPublicarRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<IniciarEnvioDoDocumentoAPublicarDto> resultado = await _commandBus.Send(
            new IniciarEnvioDoDocumentoAPublicarCommand(processoSeletivoId, request.NomeArquivo, request.TipoDeMidia), cancellationToken);
        return resultado.IsSuccess ? StatusCode(StatusCodes.Status201Created, resultado.Value) : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Passo 3: lê o arquivo do armazenamento, valida o conteúdo contra o formato declarado, calcula
    /// o SHA-256 no servidor e finaliza o documento como imutável.
    /// </summary>
    [HttpPost("{documentoAPublicarId:guid}/confirmacao")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(typeof(DocumentoAPublicarDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ConfirmarEnvio(
        Guid processoSeletivoId, Guid documentoAPublicarId, CancellationToken cancellationToken)
    {
        Result<DocumentoAPublicarDto> resultado = await _commandBus.Send(
            new ConfirmarEnvioDoDocumentoAPublicarCommand(processoSeletivoId, documentoAPublicarId), cancellationToken);
        return resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Emite o acesso de leitura a um documento confirmado — a URL assinada é
    /// gerada no pedido, com validade curta, e não vem embutida na listagem.
    /// </summary>
    /// <remarks>
    /// A resposta carrega uma credencial de acesso ao objeto e por isso vai
    /// com <c>no-store</c>: guardada em cache de proxy ou no histórico do
    /// navegador, ela continuaria abrindo o arquivo para quem alcançasse o
    /// cache, sem passar por autorização de novo.
    /// </remarks>
    [HttpGet("{documentoAPublicarId:guid}/acesso")]
    [VendorMediaType(Resource = "acesso-documento-a-publicar", Versions = [1])]
    [ProducesResponseType(typeof(AcessoDocumentoAPublicarDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Acesso(
        Guid processoSeletivoId, Guid documentoAPublicarId, CancellationToken cancellationToken)
    {
        Result<AcessoDocumentoAPublicarDto> resultado = await _queryBus
            .Send(new ObterAcessoDocumentoAPublicarQuery(processoSeletivoId, documentoAPublicarId), cancellationToken)
            .ConfigureAwait(false);

        if (!resultado.IsSuccess)
            return resultado.ToActionResult(_mapper);

        Response.Headers.CacheControl = "no-store";
        return Ok(resultado.Value);
    }
}
