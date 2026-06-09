namespace Unifesspa.UniPlus.OrganizacaoInstitucional.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Infrastructure.Core.Hateoas;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Unidades;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.DTOs;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Queries.Unidades;

/// <summary>
/// Endpoints públicos de leitura (<c>GET /api/unidades</c>,
/// <c>GET /api/unidades/{id}</c>) e endpoints admin
/// (<c>POST/PUT/DELETE /api/admin/unidades</c>) restritos a
/// <c>plataforma-admin</c>.
/// </summary>
/// <remarks>
/// Rotas público/admin em caminhos distintos — o controller declara
/// <c>[Route("api")]</c> e cada action especifica seu caminho.
/// </remarks>
[ApiController]
[Route("api")]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public.")]
public sealed class UnidadesController : ControllerBase
{
    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;
    private readonly IResourceLinksBuilder<UnidadeDto> _linksBuilder;

    public UnidadesController(
        ICommandBus commandBus,
        IQueryBus queryBus,
        IDomainErrorMapper mapper,
        IResourceLinksBuilder<UnidadeDto> linksBuilder)
    {
        _commandBus = commandBus;
        _queryBus = queryBus;
        _mapper = mapper;
        _linksBuilder = linksBuilder;
    }

    /// <summary>
    /// Lista todas as unidades organizacionais ativas. Sem cursor — catálogo é
    /// reference data bounded (exceção deliberada a ADR-0026).
    /// </summary>
    [HttpGet("unidades")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "unidade", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<UnidadeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> Listar(CancellationToken cancellationToken)
    {
        IReadOnlyList<UnidadeDto> unidades = await _queryBus
            .Send(new ListarUnidadesAtivasQuery(), cancellationToken)
            .ConfigureAwait(false);
        return Ok(unidades);
    }

    /// <summary>
    /// Obtém uma unidade pelo Id. Retorna 404 quando inexistente.
    /// </summary>
    [HttpGet("unidades/{id:guid}")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "unidade", Versions = [1])]
    [ProducesResponseType(typeof(UnidadeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken cancellationToken)
    {
        UnidadeDto? unidade = await _queryBus
            .Send(new ObterUnidadePorIdQuery(id), cancellationToken)
            .ConfigureAwait(false);

        if (unidade is null)
        {
            return NotFound();
        }

        UnidadeDto comLinks = unidade with { Links = _linksBuilder.Build(unidade) };
        return Ok(comLinks);
    }

    /// <summary>
    /// Cria uma nova unidade organizacional. Restrito a <c>plataforma-admin</c>.
    /// Idempotency-Key obrigatório (ADR-0027).
    /// </summary>
    [HttpPost("admin/unidades")]
    [Authorize(Roles = "plataforma-admin")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Criar(
        [FromBody] CriarUnidadeCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        Result<Guid> resultado = await _commandBus
            .Send(command, cancellationToken)
            .ConfigureAwait(false);

        if (resultado.IsSuccess)
        {
            return CreatedAtAction(
                nameof(ObterPorId),
                new { id = resultado.Value },
                resultado.Value);
        }

        return resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Atualiza uma unidade organizacional existente. Restrito a
    /// <c>plataforma-admin</c>.
    /// </summary>
    [HttpPut("admin/unidades/{id:guid}")]
    [Authorize(Roles = "plataforma-admin")]
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
        [FromBody] AtualizarUnidadeCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (id != command.Id)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Id divergente",
                Detail = "O Id na URL não corresponde ao Id no corpo da requisição.",
                Status = StatusCodes.Status400BadRequest,
            });
        }

        Result resultado = await _commandBus
            .Send(command, cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Remove (soft-delete) uma unidade organizacional. Restrito a
    /// <c>plataforma-admin</c>.
    /// </summary>
    [HttpDelete("admin/unidades/{id:guid}")]
    [Authorize(Roles = "plataforma-admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Remover(Guid id, CancellationToken cancellationToken)
    {
        Result resultado = await _commandBus
            .Send(new RemoverUnidadeCommand(id), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }
}
