namespace Unifesspa.UniPlus.Selecao.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Application.Commands.MotivosDoProcesso;
using Application.Commands.ProcessosSeletivos;
using Application.DTOs;
using Application.Queries.MotivosDoProcesso;

using Contracts.Requests;

using Http;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Infrastructure.Core.OpenApi;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.API.Authorization;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Os motivos de decisão que o processo escolhe do cadastro, em cada fase ou etapa (UNI-REQ-0285). No rascunho a escolha
/// é livre; depois da publicação, só a sessão de retificação inclui, com justificativa e antes de começar a avaliação
/// naquele lugar, e nada do que foi publicado se retira.
/// </summary>
[ApiController]
[EmiteEsperaEsgotadaPelaTrava]
[Route("processos-seletivos")]
[Tags("ProcessoSeletivo")]
[Authorize]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public; sem isso o MVC ignora a classe e nenhum endpoint é registrado.")]
public sealed class ProcessoSeletivoMotivosController : ControllerBase
{
    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;

    public ProcessoSeletivoMotivosController(ICommandBus commandBus, IQueryBus queryBus, IDomainErrorMapper mapper)
    {
        _commandBus = commandBus;
        _queryBus = queryBus;
        _mapper = mapper;
    }

    /// <summary>
    /// Os motivos escolhidos pelo processo em cada fase e etapa, com a justificativa interna das inclusões feitas depois
    /// da publicação — por isso a leitura exige a permissão de configurar o processo. Com <c>versao</c>, a escolha que
    /// valia naquela versão publicada do edital; sem ela, a escolha atual.
    /// </summary>
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosConfigurar)]
    [HttpGet("{id:guid}/motivos-de-decisao")]
    [VendorMediaType(Resource = "motivo-do-processo", Versions = [1])]
    [ProducesResponseType(typeof(IReadOnlyList<MotivoDoProcessoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> Listar(Guid id, [FromQuery(Name = "versao")] int? versao, CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<MotivoDoProcessoDto>> motivos = await _queryBus
            .Send(new ListarMotivosDoProcessoQuery(id, versao), cancellationToken)
            .ConfigureAwait(false);
        return motivos.IsFailure ? motivos.ToActionResult(_mapper) : Ok(motivos.Value);
    }

    /// <summary>
    /// Inclui na fase o motivo do cadastro. Sob a sessão de retificação, exige o <c>If-Match</c> dela e a justificativa, e
    /// devolve o <c>ETag</c> novo.
    /// </summary>
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosConfigurar)]
    [HttpPut("{id:guid}/fases/{faseCanonicaOrigemId:guid}/motivos-de-decisao/{motivoDeDecisaoId:guid}")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status428PreconditionRequired)]
    [EmiteETag]
    public Task<IActionResult> IncluirNaFase(
        Guid id,
        Guid faseCanonicaOrigemId,
        Guid motivoDeDecisaoId,
        [FromBody] IncluirMotivoNoProcessoRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken) =>
        IncluirAsync(id, LugarDaFase(faseCanonicaOrigemId), motivoDeDecisaoId, request, ifMatch, cancellationToken);

    /// <summary>
    /// Inclui na etapa o motivo do cadastro. Sob a sessão de retificação, exige o <c>If-Match</c> dela e a justificativa, e
    /// devolve o <c>ETag</c> novo.
    /// </summary>
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosConfigurar)]
    [HttpPut("{id:guid}/etapas/{etapaId:guid}/motivos-de-decisao/{motivoDeDecisaoId:guid}")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status428PreconditionRequired)]
    [EmiteETag]
    public Task<IActionResult> IncluirNaEtapa(
        Guid id,
        Guid etapaId,
        Guid motivoDeDecisaoId,
        [FromBody] IncluirMotivoNoProcessoRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken) =>
        IncluirAsync(id, LugarDaEtapa(etapaId), motivoDeDecisaoId, request, ifMatch, cancellationToken);

    /// <summary>
    /// Retira da fase o motivo escolhido que ainda não consta do edital publicado; o publicado não sai (UNI-REQ-0124).
    /// </summary>
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosConfigurar)]
    [HttpDelete("{id:guid}/fases/{faseCanonicaOrigemId:guid}/motivos-de-decisao/{motivoDeDecisaoId:guid}")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status428PreconditionRequired)]
    [EmiteETag]
    public Task<IActionResult> RetirarDaFase(
        Guid id,
        Guid faseCanonicaOrigemId,
        Guid motivoDeDecisaoId,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken) =>
        RetirarAsync(id, LugarDaFase(faseCanonicaOrigemId), motivoDeDecisaoId, ifMatch, cancellationToken);

    /// <summary>
    /// Retira da etapa o motivo escolhido que ainda não consta do edital publicado; o publicado não sai (UNI-REQ-0124).
    /// </summary>
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosConfigurar)]
    [HttpDelete("{id:guid}/etapas/{etapaId:guid}/motivos-de-decisao/{motivoDeDecisaoId:guid}")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status428PreconditionRequired)]
    [EmiteETag]
    public Task<IActionResult> RetirarDaEtapa(
        Guid id,
        Guid etapaId,
        Guid motivoDeDecisaoId,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken) =>
        RetirarAsync(id, LugarDaEtapa(etapaId), motivoDeDecisaoId, ifMatch, cancellationToken);

    // A identidade vazia não designa fase nem etapa: o lugar não existe, e o domínio não a aceita como lugar.
    private static LugarDaDecisao? LugarDaFase(Guid faseCanonicaOrigemId) =>
        faseCanonicaOrigemId == Guid.Empty ? null : LugarDaDecisao.DaFase(faseCanonicaOrigemId);

    private static LugarDaDecisao? LugarDaEtapa(Guid etapaId) =>
        etapaId == Guid.Empty ? null : LugarDaDecisao.DaEtapa(etapaId);

    private async Task<IActionResult> IncluirAsync(
        Guid id,
        LugarDaDecisao? lugar,
        Guid motivoDeDecisaoId,
        IncluirMotivoNoProcessoRequest request,
        string? ifMatch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (lugar is null)
            return Result.Failure(EscolhaDosMotivos.LugarNaoEncontrado()).ToActionResult(_mapper);

        if (!ProtocoloDeMutacao.TentarLerPrecondicao(_mapper, ifMatch, out PrecondicaoIfMatch precondicao, out IActionResult? malformada))
            return malformada!;

        Result<MutacaoAceita> resultado = await _commandBus.Send(
            new IncluirMotivoNoProcessoCommand(id, lugar, motivoDeDecisaoId, request.Justificativa, precondicao), cancellationToken);
        return ProtocoloDeMutacao.ResponderMutacao(_mapper, Response, resultado);
    }

    private async Task<IActionResult> RetirarAsync(
        Guid id, LugarDaDecisao? lugar, Guid motivoDeDecisaoId, string? ifMatch, CancellationToken cancellationToken)
    {
        if (lugar is null)
            return Result.Failure(EscolhaDosMotivos.LugarNaoEncontrado()).ToActionResult(_mapper);

        if (!ProtocoloDeMutacao.TentarLerPrecondicao(_mapper, ifMatch, out PrecondicaoIfMatch precondicao, out IActionResult? malformada))
            return malformada!;

        Result<MutacaoAceita> resultado = await _commandBus.Send(
            new RetirarMotivoDoProcessoCommand(id, lugar, motivoDeDecisaoId, precondicao), cancellationToken);
        return ProtocoloDeMutacao.ResponderMutacao(_mapper, Response, resultado);
    }
}
