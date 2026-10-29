namespace Unifesspa.UniPlus.Autorizacao.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Queries.Perfis;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Authorization;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Perfis de acesso sob <c>/api/autorizacao/perfis</c>.
/// </summary>
/// <remarks>
/// Todas as operações, inclusive a leitura, exigem <c>autorizacao:perfis:manter</c> pelo ponto
/// de decisão único. O <c>[Authorize]</c> sem papel na classe separa os desfechos: quem não se
/// autenticou recebe <c>401</c> da própria pipeline; quem se autenticou sem a concessão recebe
/// <c>403</c> do <c>[ExigePermissao]</c> de cada ação.
/// </remarks>
[ApiController]
[Authorize]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public.")]
public sealed class PerfisController : ControllerBase
{
    private const string ResourceTag = "perfis";

    /// <summary>
    /// Tipo de recurso que a decisão de acesso enxerga. A restrição de tipo de uma concessão é
    /// comparada por igualdade exata, então este valor é o contrato com quem restringe a
    /// concessão por tipo.
    /// </summary>
    private const string RecursoTipo = "perfil";

    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;

    public PerfisController(ICommandBus commandBus, IQueryBus queryBus, IDomainErrorMapper mapper)
    {
        _commandBus = commandBus;
        _queryBus = queryBus;
        _mapper = mapper;
    }

    /// <summary>Lista os perfis, paginados por cursor opaco bidirecional (ADR-0026 + ADR-0089).</summary>
    [HttpGet("perfis")]
    [ExigePermissao<PermissaoAutorizacao>(PermissaoAutorizacao.PerfisManter, RecursoTipo = RecursoTipo)]
    [VendorMediaType(Resource = "perfil", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<PerfilDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Listar(
        [FromCursor(ResourceTag)] PageRequest page,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);

        ListarPerfisResult resultado = await _queryBus.Send(
            new ListarPerfisQuery(page.AfterId, page.Limit, page.Direction),
            cancellationToken).ConfigureAwait(false);

        return await this.OkPaginatedAsync(
            resultado.Items,
            resultado.AnteriorAfterId,
            resultado.ProximoAfterId,
            page,
            ResourceTag,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Obtém um perfil pelo Id. Retorna 404 quando inexistente.</summary>
    [HttpGet("perfis/{id:guid}")]
    [ExigePermissao<PermissaoAutorizacao>(PermissaoAutorizacao.PerfisManter, RecursoTipo = RecursoTipo)]
    [VendorMediaType(Resource = "perfil", Versions = [1])]
    [ProducesResponseType(typeof(PerfilDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken cancellationToken)
    {
        Result<PerfilDto> resultado = await _queryBus
            .Send(new ObterPerfilQuery(id), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
    }

    /// <summary>Cria um perfil de acesso. <c>Idempotency-Key</c> obrigatório (ADR-0027).</summary>
    [HttpPost("perfis")]
    [ExigePermissao<PermissaoAutorizacao>(PermissaoAutorizacao.PerfisManter, RecursoTipo = RecursoTipo)]
    [RequiresIdempotencyKey]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Criar(
        [FromBody] CriarPerfilCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        Result<Guid> resultado = await _commandBus.Send(command, cancellationToken).ConfigureAwait(false);

        return resultado.IsSuccess
            ? CreatedAtAction(nameof(ObterPorId), new { id = resultado.Value }, resultado.Value)
            : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Substitui nome, descrição e permissões do perfil comum. Perfil de governança não se
    /// edita. <c>Idempotency-Key</c> obrigatório.
    /// </summary>
    [HttpPut("perfis/{id:guid}")]
    [ExigePermissao<PermissaoAutorizacao>(PermissaoAutorizacao.PerfisManter, RecursoTipo = RecursoTipo)]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Atualizar(
        Guid id,
        [FromBody] AtualizarPerfilCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.Id != id)
        {
            return Result.Failure(new DomainError(
                PerfilErrorCodes.IdDivergente,
                "O Id na URL não corresponde ao Id no corpo da requisição."))
                .ToActionResult(_mapper);
        }

        Result resultado = await _commandBus.Send(command, cancellationToken).ConfigureAwait(false);

        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }

    /// <summary>Reativa um perfil comum. Perfil de governança não se ativa.</summary>
    [HttpPost("perfis/{id:guid}/ativacao")]
    [ExigePermissao<PermissaoAutorizacao>(PermissaoAutorizacao.PerfisManter, RecursoTipo = RecursoTipo)]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Ativar(Guid id, CancellationToken cancellationToken)
    {
        Result resultado = await _commandBus
            .Send(new AtivarPerfilCommand(id), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Desativa um perfil comum. Não revoga concessão alguma: só impede pedidos novos.
    /// Perfil de governança não se desativa.
    /// </summary>
    [HttpDelete("perfis/{id:guid}/ativacao")]
    [ExigePermissao<PermissaoAutorizacao>(PermissaoAutorizacao.PerfisManter, RecursoTipo = RecursoTipo)]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Desativar(Guid id, CancellationToken cancellationToken)
    {
        Result resultado = await _commandBus
            .Send(new DesativarPerfilCommand(id), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }
}
