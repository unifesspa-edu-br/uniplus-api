namespace Unifesspa.UniPlus.Autorizacao.API.Controllers;


using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.RevisoesDasConcessoes;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Queries.Perfis;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A revisão das concessões e o envio de decisões, sob <c>/api/autorizacao/perfis/{id}/revisao-das-concessoes</c>.
/// </summary>
/// <remarks>
/// A permissão de cada operação é a da etapa que ela exerce, conferida no handler com o registro do desfecho.
/// </remarks>
[ApiController]
[Authorize]
public sealed class RevisaoDasConcessoesController : ControllerBase
{
    private const int TtlDaIdempotenciaEmSegundos = 15 * 60;

    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;

    public RevisaoDasConcessoesController(ICommandBus commandBus, IQueryBus queryBus, IDomainErrorMapper mapper)
    {
        _commandBus = commandBus;
        _queryBus = queryBus;
        _mapper = mapper;
    }

    /// <summary>A revisão das concessões mais recente do perfil, com cada concessão na situação em que está.</summary>
    [HttpGet("perfis/{id:guid}/revisao-das-concessoes")]
    [ProducesResponseType(typeof(RevisaoDasConcessoesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Obter(Guid id, CancellationToken cancellationToken) =>
        Resposta(await _queryBus.Send(new ObterRevisaoDasConcessoesDoPerfilQuery(id), cancellationToken).ConfigureAwait(false));

    /// <summary>Lista as revisões abertas em que quem lê tem concessão a decidir na etapa; a permissão é a da etapa.</summary>
    [HttpGet("revisoes-de-concessoes")]
    [ProducesResponseType(typeof(IReadOnlyList<ItemDaListaDeRevisoesDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Fila([FromQuery] string? etapa, CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<ItemDaListaDeRevisoesDto>> resultado = await _queryBus
            .Send(new ListarFilaDeRevisoesDasConcessoesQuery(etapa), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
    }

    /// <summary>Envia as decisões da gestão de acessos: uma decisão por concessão elegível, menos as do próprio gestor.</summary>
    [HttpPost("perfis/{id:guid}/revisao-das-concessoes/decisoes")]
    [RequiresIdempotencyKey(TtlSeconds = TtlDaIdempotenciaEmSegundos)]
    [ProducesResponseType(typeof(RevisaoDasConcessoesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> EnviarDecisoesDaGestao(Guid id, [FromBody] EnviarDecisoesDaRevisaoRequest dados, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dados);

        return Resposta(await _commandBus.Send(new EnviarDecisoesDaRevisaoCommand(id, dados), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Decide a concessão retida no envio de decisões, por outra pessoa que não o beneficiário.</summary>
    [HttpPost("perfis/{id:guid}/revisao-das-concessoes/retidas/{concessaoId:guid}")]
    [RequiresIdempotencyKey(TtlSeconds = TtlDaIdempotenciaEmSegundos)]
    [ProducesResponseType(typeof(RevisaoDasConcessoesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DecidirRetida(Guid id, Guid concessaoId, [FromBody] DecidirRetidaRequest dados, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dados);

        return Resposta(await _commandBus.Send(new DecidirRetidaDaRevisaoCommand(id, concessaoId, dados), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Decide a etapa da proteção de dados das concessões que a aguardam.</summary>
    [HttpPost("perfis/{id:guid}/revisao-das-concessoes/protecao-de-dados")]
    [RequiresIdempotencyKey(TtlSeconds = TtlDaIdempotenciaEmSegundos)]
    [ProducesResponseType(typeof(RevisaoDasConcessoesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DecidirProtecaoDeDados(Guid id, [FromBody] DecidirEtapaDaProtecaoRequest dados, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dados);

        return Resposta(await _commandBus.Send(new DecidirEtapaDaProtecaoDaRevisaoCommand(id, dados), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Encerra a revisão antes do término, com motivo: quem não migrou perde o acesso.</summary>
    [HttpPost("perfis/{id:guid}/revisao-das-concessoes/encerramento")]
    [RequiresIdempotencyKey(TtlSeconds = TtlDaIdempotenciaEmSegundos)]
    [ProducesResponseType(typeof(RevisaoDasConcessoesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Encerrar(Guid id, [FromBody] EncerrarRevisaoRequest dados, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dados);

        return Resposta(await _commandBus.Send(new EncerrarRevisaoDoPerfilCommand(id, dados), cancellationToken).ConfigureAwait(false));
    }

    private IActionResult Resposta(Result<RevisaoDasConcessoesDto> resultado) =>
        resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
}
