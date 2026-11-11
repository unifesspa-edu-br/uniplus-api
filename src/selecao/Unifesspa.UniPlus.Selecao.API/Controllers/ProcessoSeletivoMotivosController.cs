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
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Infrastructure.Core.OpenApi;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.API.Authorization;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Os motivos de decisão que o processo escolhe do cadastro, por tipo de decisão que usa
/// (UNI-REQ-0285). No rascunho a escolha é livre; depois da publicação, só a sessão de
/// retificação inclui, com justificativa e antes de começar a avaliação daquela decisão, e nada
/// do que foi publicado se retira.
/// </summary>
[ApiController]
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
    /// Os motivos escolhidos pelo processo, com a justificativa interna das inclusões feitas depois
    /// da publicação — por isso a leitura exige a permissão de configurar o processo.
    /// </summary>
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosConfigurar)]
    [HttpGet("{id:guid}/motivos-de-decisao")]
    [ProducesResponseType(typeof(IReadOnlyList<MotivoDoProcessoDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar(Guid id, CancellationToken cancellationToken)
    {
        IReadOnlyList<MotivoDoProcessoDto> motivos = await _queryBus
            .Send(new ListarMotivosDoProcessoQuery(id), cancellationToken)
            .ConfigureAwait(false);
        return Ok(motivos);
    }

    /// <summary>
    /// Inclui no processo o motivo do cadastro. Sob a sessão de retificação, exige o <c>If-Match</c>
    /// dela e a justificativa, e devolve o <c>ETag</c> novo.
    /// </summary>
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosConfigurar)]
    [HttpPut("{id:guid}/motivos-de-decisao/{motivoDeDecisaoId:guid}")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status428PreconditionRequired)]
    [EmiteETag]
    public async Task<IActionResult> Incluir(
        Guid id,
        Guid motivoDeDecisaoId,
        [FromBody] IncluirMotivoNoProcessoRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!ProtocoloDeMutacao.TentarLerPrecondicao(_mapper, ifMatch, out PrecondicaoIfMatch precondicao, out IActionResult? malformada))
            return malformada!;

        Result<MutacaoAceita> resultado = await _commandBus.Send(
            new IncluirMotivoNoProcessoCommand(id, motivoDeDecisaoId, request.Justificativa, precondicao), cancellationToken);
        return ProtocoloDeMutacao.ResponderMutacao(_mapper, Response, resultado);
    }

    /// <summary>
    /// Retira do processo o motivo escolhido que ainda não consta do edital publicado; o publicado
    /// não sai (UNI-REQ-0124).
    /// </summary>
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosConfigurar)]
    [HttpDelete("{id:guid}/motivos-de-decisao/{motivoDeDecisaoId:guid}")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status428PreconditionRequired)]
    [EmiteETag]
    public async Task<IActionResult> Retirar(
        Guid id,
        Guid motivoDeDecisaoId,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken)
    {
        if (!ProtocoloDeMutacao.TentarLerPrecondicao(_mapper, ifMatch, out PrecondicaoIfMatch precondicao, out IActionResult? malformada))
            return malformada!;

        Result<MutacaoAceita> resultado = await _commandBus.Send(
            new RetirarMotivoDoProcessoCommand(id, motivoDeDecisaoId, precondicao), cancellationToken);
        return ProtocoloDeMutacao.ResponderMutacao(_mapper, Response, resultado);
    }
}
