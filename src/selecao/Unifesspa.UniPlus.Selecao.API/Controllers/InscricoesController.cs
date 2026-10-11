namespace Unifesspa.UniPlus.Selecao.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Application.Commands.Inscricoes;
using Application.DTOs;
using Application.Queries.Inscricoes;

using Http;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A inscrição do candidato (issue #1859) num processo seletivo. A policy <c>Candidato</c>
/// (claim <c>cpf</c>) é declarada em cada action — o host não tem fallback policy, então omitir a
/// anotação nasceria o endpoint anônimo por omissão (mesma convenção de
/// <see cref="FormulariosController"/> com <c>plataforma-admin</c>). Nenhuma rota recebe id de
/// inscrição: o dono do token, pelo CPF do claim, é sempre quem resolve qual inscrição.
/// </summary>
[ApiController]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public; sem isso o MVC ignora a classe e nenhum endpoint é registrado.")]
public sealed class InscricoesController : ControllerBase
{
    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;

    public InscricoesController(ICommandBus commandBus, IQueryBus queryBus, IDomainErrorMapper mapper)
    {
        _commandBus = commandBus;
        _queryBus = queryBus;
        _mapper = mapper;
    }

    /// <summary>A inscrição do dono do token, com o <c>ETag</c> também no header, para o <c>If-Match</c> seguinte.</summary>
    [HttpGet("processos-seletivos/{id:guid}/inscricao")]
    [Authorize(Policy = "Candidato")]
    [VendorMediaType(Resource = "inscricao", Versions = [1])]
    [ProducesResponseType(typeof(InscricaoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> Obter(Guid id, CancellationToken cancellationToken)
    {
        Result<InscricaoDto> resultado = await _queryBus
            .Send(new ObterInscricaoQuery(id), cancellationToken)
            .ConfigureAwait(false);
        if (resultado.IsFailure)
        {
            return resultado.ToActionResult(_mapper);
        }

        Response.Headers.ETag = resultado.Value!.ETag;
        return Ok(resultado.Value);
    }

    /// <summary>O formulário renderizável da versão que a inscrição do dono do token fixou ao iniciar — nunca a vigente por relógio.</summary>
    [HttpGet("processos-seletivos/{id:guid}/inscricao/formulario")]
    [Authorize(Policy = "Candidato")]
    [VendorMediaType(Resource = "formulario-inscricao", Versions = [1])]
    [ProducesResponseType(typeof(FormularioRenderizavelDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ObterFormulario(Guid id, CancellationToken cancellationToken)
    {
        Result<FormularioRenderizavelDto> resultado = await _queryBus
            .Send(new ObterFormularioDaInscricaoQuery(id), cancellationToken)
            .ConfigureAwait(false);
        return resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Avalia, sem gravar nada, as respostas que o dono do token tem na tela (ADR-0143): as
    /// modalidades em que ele concorre e o que falta responder, na versão fixada ao iniciar. É prévia:
    /// não recusa por restrição nem por obrigatoriedade, só pela forma do corpo.
    /// </summary>
    [HttpPost("processos-seletivos/{id:guid}/inscricao/avaliacoes")]
    [Authorize(Policy = "Candidato")]
    [VendorMediaType(Resource = "avaliacao-da-inscricao", Versions = [1])]
    [ProducesResponseType(typeof(AvaliacaoDaInscricaoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Avaliar(Guid id, [FromBody] AvaliacaoDaInscricaoRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        Result<AvaliacaoDaInscricaoDto> resultado = await _queryBus
            .Send(new AvaliarInscricaoQuery(id, request), cancellationToken)
            .ConfigureAwait(false);
        return resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Inicia a inscrição fixando a versão do formulário divulgada neste instante, ou devolve a
    /// que já existe para o mesmo CPF e processo — idempotente por construção (UNI-REQ-0023), não
    /// por <c>Idempotency-Key</c>.
    /// </summary>
    [HttpPost("processos-seletivos/{id:guid}/inscricao")]
    [Authorize(Policy = "Candidato")]
    [VendorMediaType(Resource = "inscricao", Versions = [1])]
    [ProducesResponseType(typeof(InscricaoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> Iniciar(Guid id, CancellationToken cancellationToken)
    {
        Result<InscricaoDto> resultado = await _commandBus
            .Send(new IniciarInscricaoCommand(id), cancellationToken)
            .ConfigureAwait(false);
        return resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Salva o rascunho: validação estrutural, sem exigir obrigatórios — a validação completa é
    /// da submissão. Exige <c>If-Match</c> com o <see cref="InscricaoDto.ETag"/> corrente.
    /// </summary>
    [HttpPut("processos-seletivos/{id:guid}/inscricao")]
    [Authorize(Policy = "Candidato")]
    [VendorMediaType(Resource = "inscricao", Versions = [1])]
    [ProducesResponseType(typeof(InscricaoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status428PreconditionRequired)]
    public async Task<IActionResult> SalvarRascunho(
        Guid id,
        [FromBody] InscricaoRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        Result<PrecondicaoIfMatch> analise = IfMatchHeader.Analisar(ifMatch);
        if (analise.IsFailure)
        {
            return analise.ToActionResult(_mapper);
        }

        Result<InscricaoDto> resultado = await _commandBus
            .Send(new SalvarInscricaoCommand(id, request, analise.Value!), cancellationToken)
            .ConfigureAwait(false);
        return resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Envia ou corrige a inscrição: exige obrigatórios, restrições, impedimentos, grupos e termos
    /// — a validação completa do veredito de submissão. Exige <c>If-Match</c> com o
    /// <see cref="InscricaoDto.ETag"/> corrente, e <c>Idempotency-Key</c> (ADR-0027): a repetição da
    /// mesma chave devolve o comprovante já emitido, sem gerar outro número de inscrição.
    /// </summary>
    [HttpPost("processos-seletivos/{id:guid}/inscricao/submissao")]
    [Authorize(Policy = "Candidato")]
    [RequiresIdempotencyKey]
    [VendorMediaType(Resource = "comprovante-inscricao", Versions = [1])]
    [ProducesResponseType(typeof(ComprovanteInscricaoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status428PreconditionRequired)]
    public async Task<IActionResult> Submeter(
        Guid id,
        [FromBody] InscricaoRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        Result<PrecondicaoIfMatch> analise = IfMatchHeader.Analisar(ifMatch);
        if (analise.IsFailure)
        {
            return analise.ToActionResult(_mapper);
        }

        Result<ComprovanteInscricaoDto> resultado = await _commandBus
            .Send(new SubmeterInscricaoCommand(id, request, analise.Value!), cancellationToken)
            .ConfigureAwait(false);
        return resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// O comprovante número <paramref name="numero"/> da inscrição do dono do token (issue #1860):
    /// a primeira submissão é o comprovante 1, cada correção emite o próximo — append-only, o
    /// anterior nunca muda.
    /// </summary>
    [HttpGet("processos-seletivos/{id:guid}/inscricao/comprovantes/{numero:int}")]
    [Authorize(Policy = "Candidato")]
    [VendorMediaType(Resource = "comprovante-inscricao", Versions = [1])]
    [ProducesResponseType(typeof(ComprovanteInscricaoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ObterComprovante(Guid id, int numero, CancellationToken cancellationToken)
    {
        Result<ComprovanteInscricaoDto> resultado = await _queryBus
            .Send(new ObterComprovanteQuery(id, numero), cancellationToken)
            .ConfigureAwait(false);
        return resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
    }

    /// <summary>As inscrições do dono do token em qualquer processo seletivo (issue #1860), com o nome preferido.</summary>
    [HttpGet("minhas-inscricoes")]
    [Authorize(Policy = "Candidato")]
    [VendorMediaType(Resource = "minhas-inscricoes", Versions = [1])]
    [ProducesResponseType(typeof(IReadOnlyList<MinhaInscricaoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> MinhasInscricoes(CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<MinhaInscricaoDto>> resultado = await _queryBus
            .Send(new MinhasInscricoesQuery(), cancellationToken)
            .ConfigureAwait(false);
        return resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
    }
}
