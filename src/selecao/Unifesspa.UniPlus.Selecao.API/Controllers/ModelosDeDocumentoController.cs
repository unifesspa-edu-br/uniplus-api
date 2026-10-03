namespace Unifesspa.UniPlus.Selecao.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Application.Commands.ModelosDeDocumento;
using Application.DTOs;
using Application.Queries.ModelosDeDocumento;
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
/// Envio direto do modelo de documento que uma exigência oferece ao candidato (UNI-REQ-0016), por
/// URL pré-assinada do MinIO — o arquivo nunca trafega pela API. Fluxo em 3 passos: iniciar (aqui)
/// → PUT direto ao MinIO → confirmar (aqui). O modelo confirmado é o que a exigência referencia.
/// </summary>
[ApiController]
[Route("processos-seletivos/{processoSeletivoId:guid}/modelos-de-documento")]
[Authorize(Roles = "plataforma-admin")]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public; sem isso o MVC ignora a classe e nenhum endpoint é registrado.")]
public sealed class ModelosDeDocumentoController : ControllerBase
{
    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;

    public ModelosDeDocumentoController(ICommandBus commandBus, IQueryBus queryBus, IDomainErrorMapper mapper)
    {
        _commandBus = commandBus;
        _queryBus = queryBus;
        _mapper = mapper;
    }

    /// <summary>
    /// Passo 1: cria o modelo pendente com o nome e o formato e devolve a URL pré-assinada de PUT,
    /// o content-type do envio e o id do modelo.
    /// </summary>
    [HttpPost]
    [RequiresIdempotencyKey(TtlSeconds = PrazosDoArquivoEnviado.EnvioSegundos)]
    [ProducesResponseType(typeof(IniciarEnvioDoModeloDeDocumentoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> IniciarEnvio(
        Guid processoSeletivoId, [FromBody] IniciarEnvioDoModeloDeDocumentoRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<IniciarEnvioDoModeloDeDocumentoDto> resultado = await _commandBus.Send(
            new IniciarEnvioDoModeloDeDocumentoCommand(processoSeletivoId, request.NomeArquivo, request.Formato), cancellationToken);
        return resultado.IsSuccess ? StatusCode(StatusCodes.Status201Created, resultado.Value) : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Passo 3: lê o arquivo do MinIO, valida o conteúdo contra o formato, calcula o SHA-256 no
    /// servidor e finaliza o modelo como imutável.
    /// </summary>
    [HttpPost("{modeloDeDocumentoId:guid}/confirmacao")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(typeof(ModeloDeDocumentoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ConfirmarEnvio(Guid processoSeletivoId, Guid modeloDeDocumentoId, CancellationToken cancellationToken)
    {
        Result<ModeloDeDocumentoDto> resultado = await _commandBus.Send(
            new ConfirmarEnvioDoModeloDeDocumentoCommand(processoSeletivoId, modeloDeDocumentoId), cancellationToken);
        return resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Emite o acesso de leitura a um modelo confirmado, gerado no pedido e com validade curta.
    /// A resposta carrega uma credencial de acesso ao objeto e por isso vai com <c>no-store</c>.
    /// </summary>
    [HttpGet("{modeloDeDocumentoId:guid}/acesso")]
    [VendorMediaType(Resource = "acesso-modelo-de-documento", Versions = [1])]
    [ProducesResponseType(typeof(AcessoModeloDeDocumentoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Acesso(Guid processoSeletivoId, Guid modeloDeDocumentoId, CancellationToken cancellationToken)
    {
        Result<AcessoModeloDeDocumentoDto> resultado = await _queryBus
            .Send(new ObterAcessoModeloDeDocumentoQuery(processoSeletivoId, modeloDeDocumentoId), cancellationToken)
            .ConfigureAwait(false);
        if (!resultado.IsSuccess)
        {
            return resultado.ToActionResult(_mapper);
        }

        Response.Headers.CacheControl = "no-store";
        return Ok(resultado.Value);
    }
}
