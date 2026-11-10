namespace Unifesspa.UniPlus.Autorizacao.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Permissoes;
using Unifesspa.UniPlus.Autorizacao.Application.Queries.Perfis;
using Unifesspa.UniPlus.Infrastructure.Core.Authorization;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A contenção do perfil: suspender e retomar, desativar e reativar, e a trilha de tudo isso, sob
/// <c>/api/autorizacao/perfis/{id}</c>. Todas as operações levam motivo.
/// </summary>
/// <remarks>
/// A suspensão e a retomada exigem manter perfis ou aprovar pela proteção de dados, conferidas no handler porque a
/// permissão da retomada é a da etapa; a leitura da trilha exige uma das duas. A desativação e a reativação exigem
/// <c>autorizacao:perfis:manter</c> na borda.
/// </remarks>
[ApiController]
[Authorize]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public.")]
public sealed class ContencaoDoPerfilController : ControllerBase
{
    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;

    public ContencaoDoPerfilController(ICommandBus commandBus, IQueryBus queryBus, IDomainErrorMapper mapper)
    {
        _commandBus = commandBus;
        _queryBus = queryBus;
        _mapper = mapper;
    }

    /// <summary>
    /// Suspende o perfil, com motivo: nenhuma concessão dele vale na decisão de acesso, sem ser revogada, até a retomada. A
    /// gestão de acessos ou a proteção de dados suspendem, e a suspensão avisa os outros gestores.
    /// </summary>
    [HttpPost("perfis/{id:guid}/suspensao")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Suspender(Guid id, [FromBody] MotivoDaContencaoRequest dados, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dados);

        return SemCorpo(await _commandBus.Send(new SuspenderPerfilCommand(id, dados.Motivo), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Decide uma etapa da retomada, com motivo: a gestão de acessos, com manter perfis, e, quando o perfil é protegido, a
    /// proteção de dados. Quem suspendeu não decide, e as duas etapas são de pessoas diferentes.
    /// </summary>
    [HttpPost("perfis/{id:guid}/suspensao/retomada")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(typeof(RetomadaDaSuspensaoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Retomar(Guid id, [FromBody] RetomadaDaSuspensaoRequest dados, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dados);

        Result<RetomadaDaSuspensaoDto> resultado = await _commandBus
            .Send(new RetomarSuspensaoDoPerfilCommand(id, dados.Etapa, dados.Motivo), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
    }

    /// <summary>Desativa um perfil comum, com motivo. Não revoga concessão alguma: só impede pedidos e versões novos.</summary>
    [HttpPost("perfis/{id:guid}/desativacao")]
    [ExigePermissao<PermissaoAutorizacao>(PermissaoAutorizacao.PerfisManter, ResourceTipo = ResourceTiposAutorizacao.Perfil)]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Desativar(Guid id, [FromBody] MotivoDaContencaoRequest dados, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dados);

        return SemCorpo(await _commandBus.Send(new DesativarPerfilCommand(id, dados.Motivo), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Reativa um perfil desativado, com motivo, por outra pessoa que não quem o desativou.</summary>
    [HttpPost("perfis/{id:guid}/reativacao")]
    [ExigePermissao<PermissaoAutorizacao>(PermissaoAutorizacao.PerfisManter, ResourceTipo = ResourceTiposAutorizacao.Perfil)]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Reativar(Guid id, [FromBody] MotivoDaContencaoRequest dados, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dados);

        return SemCorpo(await _commandBus.Send(new ReativarPerfilCommand(id, dados.Motivo), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// A trilha da contenção do perfil, na ordem em que foi gravada: suspensões, decisões de cada etapa da retomada,
    /// retomadas, desativações e reativações, com o motivo e as pessoas.
    /// </summary>
    [HttpGet("perfis/{id:guid}/contencao")]
    [ProducesResponseType(typeof(IReadOnlyList<RegistroDaContencaoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterTrilha(Guid id, CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<RegistroDaContencaoDto>> resultado = await _queryBus
            .Send(new ObterContencaoDoPerfilQuery(id), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
    }

    private IActionResult SemCorpo(Result resultado) => resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
}
