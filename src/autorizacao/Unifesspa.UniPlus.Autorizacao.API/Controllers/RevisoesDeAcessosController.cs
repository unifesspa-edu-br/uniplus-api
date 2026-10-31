namespace Unifesspa.UniPlus.Autorizacao.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Revisoes;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Queries.Revisoes;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Revisão periódica dos acessos a dado pessoal ou sensível sob <c>/api/autorizacao/revisoes</c> (ADR-0152): a proteção
/// de dados abre a revisão e decide os itens; os gestores de acessos decidem os itens da própria proteção de dados.
/// </summary>
/// <remarks>
/// As rotas exigem só autenticação, porque a permissão que vale depende do item: o handler a confere pelo ponto de
/// decisão único. Sem nenhuma permissão da revisão a resposta é <c>403</c>, com a da outra governança é recusa de item, e a
/// identidade incompleta é <c>401</c>.
/// </remarks>
[ApiController]
[Authorize]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public.")]
public sealed class RevisoesDeAcessosController : ControllerBase
{
    private const string ResourceTag = "revisoes-de-acessos";

    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;

    public RevisoesDeAcessosController(ICommandBus commandBus, IQueryBus queryBus, IDomainErrorMapper mapper)
    {
        _commandBus = commandBus;
        _queryBus = queryBus;
        _mapper = mapper;
    }

    /// <summary>
    /// Abre a revisão: um item por concessão ativa de perfil pessoal ou sensível. Só uma revisão fica em andamento por vez.
    /// <c>Idempotency-Key</c> obrigatório.
    /// </summary>
    [HttpPost("revisoes")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(typeof(RevisaoDeAcessosDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Abrir(CancellationToken cancellationToken)
    {
        Result<RevisaoDeAcessosDto> resultado = await _commandBus
            .Send(new AbrirRevisaoDeAcessosCommand(), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess
            ? CreatedAtAction(nameof(Obter), new { id = resultado.Value!.Id }, resultado.Value)
            : resultado.ToActionResult(_mapper);
    }

    /// <summary>Lista as revisões, paginadas por cursor opaco bidirecional (ADR-0026 + ADR-0089).</summary>
    [HttpGet("revisoes")]
    [VendorMediaType(Resource = "revisao-de-acessos", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<RevisaoResumoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    public async Task<IActionResult> Listar([FromCursor(ResourceTag)] PageRequest page, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);

        Result<ListarRevisoesResult> resultado = await _queryBus
            .Send(new ListarRevisoesQuery(page.AfterId, page.Limit, page.Direction), cancellationToken)
            .ConfigureAwait(false);
        if (resultado.IsFailure)
        {
            return resultado.ToActionResult(_mapper);
        }

        return await this.OkPaginatedAsync(
            resultado.Value!.Items,
            resultado.Value.AnteriorAfterId,
            resultado.Value.ProximoAfterId,
            page,
            ResourceTag,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Lê a revisão com os itens: concessão, quem decide, resultado, decisor e data.</summary>
    [HttpGet("revisoes/{id:guid}")]
    [VendorMediaType(Resource = "revisao-de-acessos", Versions = [1])]
    [ProducesResponseType(typeof(RevisaoDeAcessosDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> Obter(Guid id, CancellationToken cancellationToken)
    {
        Result<RevisaoDeAcessosDto> resultado = await _queryBus
            .Send(new ObterRevisaoQuery(id), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Confirma o item: a concessão segue ativa e a decisão entra na trilha dela. Ninguém decide o item da própria
    /// concessão. <c>Idempotency-Key</c> obrigatório.
    /// </summary>
    [HttpPost("revisoes/{id:guid}/itens/{item:guid}/confirmacao")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Confirmar(Guid id, Guid item, CancellationToken cancellationToken)
    {
        Result resultado = await _commandBus
            .Send(new ConfirmarItemDaRevisaoCommand(id, item), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Revoga a concessão do item, com efeito imediato. A concessão de gestor de acessos não se revoga se deixaria menos
    /// de dois gestores. <c>Idempotency-Key</c> obrigatório.
    /// </summary>
    [HttpPost("revisoes/{id:guid}/itens/{item:guid}/revogacao")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Revogar(
        Guid id, Guid item, [FromBody] RevogarItemDaRevisaoRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result resultado = await _commandBus
            .Send(new RevogarItemDaRevisaoCommand(id, item, request.Motivo), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }
}
