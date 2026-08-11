namespace Unifesspa.UniPlus.Configuracao.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.Commands.TiposEtapa;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Queries.TiposEtapa;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Infrastructure.Core.Hateoas;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Leitura pública de itens ativos e manutenção por plataforma-admin (UNI-REQ-0015, UNI-REQ-0087).</summary>
[ApiController]
[SuppressMessage("Performance", "CA1515:Consider making public types internal", Justification = "Controllers MVC são descobertos por reflection.")]
public sealed class TiposEtapaController : ControllerBase
{
    private const string ResourceTag = "tipos-etapa";
    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;
    private readonly IResourceLinksBuilder<TipoEtapaDto> _linksBuilder;

    public TiposEtapaController(ICommandBus commandBus, IQueryBus queryBus, IDomainErrorMapper mapper, IResourceLinksBuilder<TipoEtapaDto> linksBuilder)
    {
        _commandBus = commandBus;
        _queryBus = queryBus;
        _mapper = mapper;
        _linksBuilder = linksBuilder;
    }

    [HttpGet("tipos-etapa")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "tipo-etapa", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<TipoEtapaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Listar([FromCursor(ResourceTag)] PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        ListarTiposEtapaResult resultado = await _queryBus.Send(
            new ListarTiposEtapaQuery(page.AfterId, page.Limit, page.Direction), cancellationToken).ConfigureAwait(false);
        TipoEtapaDto[] items = [.. resultado.Items.Select(tipo => tipo with { Links = _linksBuilder.Build(tipo) })];
        return await this.OkPaginatedAsync(items, resultado.AnteriorAfterId, resultado.ProximoAfterId, page, ResourceTag, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    [HttpGet("tipos-etapa/{id:guid}")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "tipo-etapa", Versions = [1])]
    [ProducesResponseType(typeof(TipoEtapaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken cancellationToken)
    {
        TipoEtapaDto? tipo = await _queryBus.Send(new ObterTipoEtapaPorIdQuery(id), cancellationToken).ConfigureAwait(false);
        return tipo is null ? NotFound() : Ok(tipo with { Links = _linksBuilder.Build(tipo) });
    }

    [HttpPost("admin/tipos-etapa")]
    [Authorize(Roles = "plataforma-admin")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Criar([FromBody] CriarTipoEtapaCommand command, CancellationToken cancellationToken)
    {
        Result<Guid> resultado = await _commandBus.Send(command, cancellationToken).ConfigureAwait(false);
        return resultado.IsSuccess ? CreatedAtAction(nameof(ObterPorId), new { id = resultado.Value }, resultado.Value) : resultado.ToActionResult(_mapper);
    }

    [HttpPut("admin/tipos-etapa/{id:guid}")]
    [Authorize(Roles = "plataforma-admin")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Atualizar(Guid id, [FromBody] AtualizarTipoEtapaCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (id != command.Id)
        {
            return BadRequest(new ProblemDetails { Title = "Id divergente", Detail = "O Id na URL não corresponde ao Id no corpo da requisição.", Status = StatusCodes.Status400BadRequest });
        }
        Result resultado = await _commandBus.Send(command, cancellationToken).ConfigureAwait(false);
        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }

    [HttpDelete("admin/tipos-etapa/{id:guid}")]
    [Authorize(Roles = "plataforma-admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Desativar(Guid id, CancellationToken cancellationToken)
    {
        Result resultado = await _commandBus.Send(new DesativarTipoEtapaCommand(id), cancellationToken).ConfigureAwait(false);
        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }
}
