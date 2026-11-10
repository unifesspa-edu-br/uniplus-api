namespace Unifesspa.UniPlus.Autorizacao.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Janelas;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Queries.Perfis;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A janela de migração das concessões e o lote, sob <c>/api/autorizacao/perfis/{id}/janela</c>.
/// </summary>
/// <remarks>
/// A permissão de cada operação é a da etapa que ela exerce, conferida no handler com o registro do desfecho.
/// </remarks>
[ApiController]
[Authorize]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public.")]
public sealed class JanelaDeMigracaoController : ControllerBase
{
    private const int TtlDaIdempotenciaEmSegundos = 15 * 60;

    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;

    public JanelaDeMigracaoController(ICommandBus commandBus, IQueryBus queryBus, IDomainErrorMapper mapper)
    {
        _commandBus = commandBus;
        _queryBus = queryBus;
        _mapper = mapper;
    }

    /// <summary>A janela de migração mais recente do perfil, com cada concessão na situação em que está.</summary>
    [HttpGet("perfis/{id:guid}/janela")]
    [ProducesResponseType(typeof(JanelaDeMigracaoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obter(Guid id, CancellationToken cancellationToken) =>
        Resposta(await _queryBus.Send(new ObterJanelaDoPerfilQuery(id), cancellationToken).ConfigureAwait(false));

    /// <summary>Lista as janelas abertas em que quem lê tem concessão a decidir na etapa; a permissão é a da etapa.</summary>
    [HttpGet("janelas-de-migracao")]
    [ProducesResponseType(typeof(IReadOnlyList<ItemDaFilaDeJanelasDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Fila([FromQuery] string? etapa, CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<ItemDaFilaDeJanelasDto>> resultado = await _queryBus
            .Send(new ListarFilaDeJanelasQuery(etapa), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
    }

    /// <summary>Envia o lote da gestão de acessos: uma decisão por concessão elegível, menos as do próprio gestor.</summary>
    [HttpPost("perfis/{id:guid}/janela/lote")]
    [RequiresIdempotencyKey(TtlSeconds = TtlDaIdempotenciaEmSegundos)]
    [ProducesResponseType(typeof(JanelaDeMigracaoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> EnviarLote(Guid id, [FromBody] EnviarLoteDaMigracaoRequest dados, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dados);

        return Resposta(await _commandBus.Send(new EnviarLoteDaJanelaCommand(id, dados), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Decide a concessão que o lote reteve, por outra pessoa que não o beneficiário.</summary>
    [HttpPost("perfis/{id:guid}/janela/retidas/{concessaoId:guid}")]
    [RequiresIdempotencyKey(TtlSeconds = TtlDaIdempotenciaEmSegundos)]
    [ProducesResponseType(typeof(JanelaDeMigracaoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DecidirRetida(Guid id, Guid concessaoId, [FromBody] DecidirRetidaRequest dados, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dados);

        return Resposta(await _commandBus.Send(new DecidirRetidaDaJanelaCommand(id, concessaoId, dados), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Decide a etapa da proteção de dados das concessões que a aguardam.</summary>
    [HttpPost("perfis/{id:guid}/janela/protecao-de-dados")]
    [RequiresIdempotencyKey(TtlSeconds = TtlDaIdempotenciaEmSegundos)]
    [ProducesResponseType(typeof(JanelaDeMigracaoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DecidirProtecaoDeDados(Guid id, [FromBody] DecidirEtapaDaProtecaoRequest dados, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dados);

        return Resposta(await _commandBus.Send(new DecidirEtapaDaProtecaoDaJanelaCommand(id, dados), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Encerra a janela antes do prazo, com motivo: quem não migrou perde o acesso.</summary>
    [HttpPost("perfis/{id:guid}/janela/encerramento")]
    [RequiresIdempotencyKey(TtlSeconds = TtlDaIdempotenciaEmSegundos)]
    [ProducesResponseType(typeof(JanelaDeMigracaoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Encerrar(Guid id, [FromBody] EncerrarJanelaRequest dados, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dados);

        return Resposta(await _commandBus.Send(new EncerrarJanelaDoPerfilCommand(id, dados), cancellationToken).ConfigureAwait(false));
    }

    private IActionResult Resposta(Result<JanelaDeMigracaoDto> resultado) =>
        resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
}
