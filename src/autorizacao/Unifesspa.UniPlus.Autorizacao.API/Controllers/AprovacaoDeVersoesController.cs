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
/// A governança das versões do perfil: aprovar, recusar e cancelar a versão pendente, retirar permissões na hora e a fila
/// de cada etapa, sob <c>/api/autorizacao</c>.
/// </summary>
/// <remarks>
/// A permissão da aprovação, da recusa e da fila é a da etapa, conferida no handler; o cancelamento é de quem criou a
/// versão ou de quem mantém perfis, também conferido no handler. Só a remoção exige <c>autorizacao:perfis:manter</c> na
/// borda.
/// </remarks>
[ApiController]
[Authorize]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public.")]
public sealed class AprovacaoDeVersoesController : ControllerBase
{
    private const int TtlDaRemocaoEmSegundos = 15 * 60;

    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;

    public AprovacaoDeVersoesController(ICommandBus commandBus, IQueryBus queryBus, IDomainErrorMapper mapper)
    {
        _commandBus = commandBus;
        _queryBus = queryBus;
        _mapper = mapper;
    }

    /// <summary>Retira permissões da versão em vigor, na hora e sem aprovação, com motivo.</summary>
    [HttpPost("perfis/{id:guid}/remocoes")]
    [ExigePermissao<PermissaoAutorizacao>(PermissaoAutorizacao.PerfisManter, ResourceTipo = ResourceTiposAutorizacao.Perfil)]
    [RequiresIdempotencyKey(TtlSeconds = TtlDaRemocaoEmSegundos)]
    [ProducesResponseType(typeof(PerfilDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Remover(Guid id, [FromBody] RemoverPermissoesRequest dados, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dados);

        return Resposta(await _commandBus.Send(new RemoverPermissoesDoPerfilCommand(id, dados), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Aprova uma etapa da aprovação da versão pendente; a permissão é a da etapa.</summary>
    [HttpPost("perfis/{id:guid}/versoes/{versaoId:guid}/aprovacao")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(typeof(PerfilDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Aprovar(
        Guid id, Guid versaoId, [FromBody] AprovarEtapaDaVersaoRequest dados, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dados);

        return Resposta(await _commandBus
            .Send(new AprovarVersaoDoPerfilCommand(id, versaoId, dados.Etapa), cancellationToken)
            .ConfigureAwait(false));
    }

    /// <summary>Recusa uma etapa da aprovação da versão pendente, com motivo; a permissão é a da etapa.</summary>
    [HttpPost("perfis/{id:guid}/versoes/{versaoId:guid}/recusa")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(typeof(PerfilDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Recusar(
        Guid id, Guid versaoId, [FromBody] RecusarEtapaDaVersaoRequest dados, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dados);

        return Resposta(await _commandBus
            .Send(new RecusarVersaoDoPerfilCommand(id, versaoId, dados.Etapa, dados.Motivo), cancellationToken)
            .ConfigureAwait(false));
    }

    /// <summary>Cancela a versão pendente, com motivo: quem a criou ou quem mantém perfis.</summary>
    [HttpPost("perfis/{id:guid}/versoes/{versaoId:guid}/cancelamento")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(typeof(PerfilDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Cancelar(
        Guid id, Guid versaoId, [FromBody] CancelarVersaoDoPerfilRequest dados, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dados);

        return Resposta(await _commandBus
            .Send(new CancelarVersaoDoPerfilCommand(id, versaoId, dados.Motivo), cancellationToken)
            .ConfigureAwait(false));
    }

    /// <summary>Lista as versões pendentes que aguardam a etapa da fila; a permissão é a da etapa.</summary>
    [HttpGet("versoes-de-perfil")]
    [ProducesResponseType(typeof(IReadOnlyList<ItemDaFilaDeVersoesDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Fila([FromQuery] string? fila, CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<ItemDaFilaDeVersoesDto>> resultado = await _queryBus
            .Send(new ListarFilaDeVersoesQuery(fila), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
    }

    private IActionResult Resposta(Result<PerfilDto> resultado) =>
        resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
}
