namespace Unifesspa.UniPlus.Configuracao.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.Commands.BaseLegalDoFada;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Permissoes;
using Unifesspa.UniPlus.Configuracao.Application.Queries.BaseLegalDoFada;
using Unifesspa.UniPlus.Infrastructure.Core.Authorization;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Infrastructure.Core.Hateoas;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Results;

[ApiController]
[Authorize]
[SuppressMessage("Performance", "CA1515:Consider making public types internal", Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public.")]
public sealed class BaseLegalDoFadaController : ControllerBase
{
    private const string ResourceTag = "base-legal-do-fada";

    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;
    private readonly IResourceLinksBuilder<BaseLegalDoFadaDto> _linksBuilder;

    public BaseLegalDoFadaController(
        ICommandBus commandBus,
        IQueryBus queryBus,
        IDomainErrorMapper mapper,
        IResourceLinksBuilder<BaseLegalDoFadaDto> linksBuilder)
    {
        _commandBus = commandBus;
        _queryBus = queryBus;
        _mapper = mapper;
        _linksBuilder = linksBuilder;
    }

    [HttpGet("bases-legais-fada")]
    [EndpointName("ListarBasesLegaisDoFada")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "base-legal-do-fada", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<BaseLegalDoFadaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Listar(
        [FromCursor(ResourceTag)] PageRequest page,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);

        ListarBasesLegaisDoFadaResult resultado = await _queryBus
            .Send(new ListarBasesLegaisDoFadaQuery(page.AfterId, page.Limit, page.Direction), cancellationToken)
            .ConfigureAwait(false);

        BaseLegalDoFadaDto[] comLinks =
            [.. resultado.Itens.Select(t => t with { Links = _linksBuilder.Build(t) })];

        return await this.OkPaginatedAsync(
            comLinks, resultado.AnteriorAfterId, resultado.ProximoAfterId, page,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    [HttpGet("bases-legais-fada/{id:guid}")]
    [EndpointName("ObterBaseLegalDoFada")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "base-legal-do-fada", Versions = [1])]
    [ProducesResponseType(typeof(BaseLegalDoFadaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken cancellationToken)
    {
        BaseLegalDoFadaDto? entity = await _queryBus
            .Send(new ObterBaseLegalDoFadaPorIdQuery(id), cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return NotFound();
        }

        BaseLegalDoFadaDto comLinks = entity with { Links = _linksBuilder.Build(entity) };
        return Ok(comLinks);
    }

    [HttpPost("admin/bases-legais-fada")]
    [EndpointName("CadastrarBaseLegalDoFada")]
    [ExigePermissao<PermissaoConfiguracao>(PermissaoConfiguracao.BaseLegalDoFadaManter, ResourceTipo = ResourceTiposConfiguracao.BaseLegalDoFada)]
    [RequiresIdempotencyKey]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Criar(
        [FromBody] CriarBaseLegalDoFadaCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        Result<Guid> resultado = await _commandBus
            .Send(command, cancellationToken)
            .ConfigureAwait(false);

        if (resultado.IsSuccess)
        {
            return CreatedAtAction(nameof(ObterPorId), new { id = resultado.Value }, resultado.Value);
        }

        return resultado.ToActionResult(_mapper);
    }

    [HttpPut("admin/bases-legais-fada/{id:guid}")]
    [EndpointName("AlterarBaseLegalDoFada")]
    [ExigePermissao<PermissaoConfiguracao>(PermissaoConfiguracao.BaseLegalDoFadaManter, ResourceTipo = ResourceTiposConfiguracao.BaseLegalDoFada)]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Atualizar(
        Guid id,
        [FromBody] AtualizarBaseLegalDoFadaCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (this.ConferirIdDaRota(id, command.Id, _mapper) is { } divergente)
        {
            return divergente;
        }

        Result resultado = await _commandBus
            .Send(command, cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }

    [HttpDelete("admin/bases-legais-fada/{id:guid}")]
    [EndpointName("ExcluirBaseLegalDoFada")]
    [ExigePermissao<PermissaoConfiguracao>(PermissaoConfiguracao.BaseLegalDoFadaManter, ResourceTipo = ResourceTiposConfiguracao.BaseLegalDoFada)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remover(Guid id, CancellationToken cancellationToken)
    {
        Result resultado = await _commandBus
            .Send(new DesativarBaseLegalDoFadaCommand(id), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }

}
