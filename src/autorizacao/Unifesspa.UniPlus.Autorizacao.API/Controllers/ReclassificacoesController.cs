namespace Unifesspa.UniPlus.Autorizacao.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Reclassificacoes;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Queries.Reclassificacoes;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A revisão das permissões reclassificadas pelo catálogo, sob <c>/api/autorizacao/reclassificacoes</c>, e as divergências
/// da governança.
/// </summary>
/// <remarks>
/// A permissão de cada rota é a da etapa que ela exerce, conferida no handler com o registro do desfecho.
/// </remarks>
[ApiController]
[Authorize]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public.")]
public sealed class ReclassificacoesController : ControllerBase
{
    private const int TtlDaIdempotenciaEmSegundos = 15 * 60;

    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;

    public ReclassificacoesController(ICommandBus commandBus, IQueryBus queryBus, IDomainErrorMapper mapper)
    {
        _commandBus = commandBus;
        _queryBus = queryBus;
        _mapper = mapper;
    }

    /// <summary>Lista as reclassificações com concessões suspensas que ainda aguardam a etapa; a permissão é a da etapa.</summary>
    [HttpGet("reclassificacoes")]
    [ProducesResponseType(typeof(IReadOnlyList<ItemDaFilaDeReclassificacoesDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Listar([FromQuery] string? etapa, CancellationToken cancellationToken) =>
        Resposta(await _queryBus.Send(new ListarReclassificacoesQuery(etapa), cancellationToken).ConfigureAwait(false));

    /// <summary>O detalhe da reclassificação: as permissões de cada versão em uso, o documento e as concessões suspensas.</summary>
    [HttpGet("reclassificacoes/{id:guid}")]
    [ProducesResponseType(typeof(ReclassificacaoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Obter(Guid id, CancellationToken cancellationToken) =>
        Resposta(await _queryBus.Send(new ObterReclassificacaoQuery(id), cancellationToken).ConfigureAwait(false));

    /// <summary>Decide uma etapa da liberação, de forma atômica, para as concessões da lista ou para todas as que a aguardam.</summary>
    [HttpPost("reclassificacoes/{id:guid}/decisoes")]
    [RequiresIdempotencyKey(TtlSeconds = TtlDaIdempotenciaEmSegundos)]
    [ProducesResponseType(typeof(DecisaoDaReclassificacaoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Decidir(Guid id, [FromBody] DecidirReclassificacaoRequest dados, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dados);

        return Resposta(await _commandBus.Send(new DecidirReclassificacaoCommand(id, dados), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>As divergências da governança, da mais recente para a mais antiga.</summary>
    [HttpGet("governanca/divergencias")]
    [ProducesResponseType(typeof(IReadOnlyList<DivergenciaDaGovernancaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Divergencias(CancellationToken cancellationToken) =>
        Resposta(await _queryBus.Send(new ListarDivergenciasDaGovernancaQuery(), cancellationToken).ConfigureAwait(false));

    private IActionResult Resposta<T>(Result<T> resultado) =>
        resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
}
