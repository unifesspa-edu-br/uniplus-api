namespace Unifesspa.UniPlus.Configuracao.API.Controllers;


using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.Commands.TiposConceito;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Permissoes;
using Unifesspa.UniPlus.Configuracao.Application.Queries.TiposConceito;
using Unifesspa.UniPlus.Infrastructure.Core.Authorization;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Infrastructure.Core.Hateoas;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Leitura pública dos tipos de conceito ativos e manutenção por permissão.</summary>
[ApiController]
[Authorize]
public sealed class TiposConceitoController : ControllerBase
{
    private const string ResourceTag = "tipos-conceito";

    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;
    private readonly IResourceLinksBuilder<TipoConceitoDto> _linksBuilder;

    public TiposConceitoController(ICommandBus commandBus, IQueryBus queryBus, IDomainErrorMapper mapper, IResourceLinksBuilder<TipoConceitoDto> linksBuilder)
    {
        _commandBus = commandBus;
        _queryBus = queryBus;
        _mapper = mapper;
        _linksBuilder = linksBuilder;
    }

    [HttpGet("tipos-conceito")]
    [EndpointName("ListarTiposDeConceito")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "tipo-conceito", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<TipoConceitoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Listar([FromCursor(ResourceTag)] PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        ListarTiposConceitoResult resultado = await _queryBus.Send(
            new ListarTiposConceitoQuery(page.AfterId, page.Limit, page.Direction), cancellationToken).ConfigureAwait(false);
        TipoConceitoDto[] items = [.. resultado.Items.Select(tipo => tipo with { Links = _linksBuilder.Build(tipo) })];
        return await this.OkPaginatedAsync(items, resultado.AnteriorAfterId, resultado.ProximoAfterId, page, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    [HttpGet("tipos-conceito/{id:guid}")]
    [EndpointName("ObterTipoDeConceito")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "tipo-conceito", Versions = [1])]
    [ProducesResponseType(typeof(TipoConceitoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken cancellationToken)
    {
        TipoConceitoDto? tipo = await _queryBus.Send(new ObterTipoConceitoPorIdQuery(id), cancellationToken).ConfigureAwait(false);
        return tipo is null ? NotFound() : Ok(tipo with { Links = _linksBuilder.Build(tipo) });
    }

    /// <summary>
    /// Cria um tipo de conceito. O corpo leva o <c>codigo</c>, que não muda depois, e a <c>definicao</c>, na forma que a
    /// resposta devolve, com <c>natureza</c> pelo código.
    /// </summary>
    [HttpPost("admin/tipos-conceito")]
    [EndpointName("CadastrarTipoDeConceito")]
    [ExigePermissao<PermissaoConfiguracao>(PermissaoConfiguracao.TiposConceitoManter, ResourceTipo = ResourceTiposConfiguracao.TipoConceito)]
    [RequiresIdempotencyKey]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Criar([FromBody] CriarTipoConceitoCommand command, CancellationToken cancellationToken)
    {
        Result<Guid> resultado = await _commandBus.Send(command, cancellationToken).ConfigureAwait(false);
        return resultado.IsSuccess ? CreatedAtAction(nameof(ObterPorId), new { id = resultado.Value }, resultado.Value) : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Substitui a definição do tipo de conceito; o código é imutável. O corpo leva o <c>id</c> da rota e a <c>definicao</c>,
    /// que é a mesma que a resposta devolve, com <c>natureza</c> igual a <c>definicao.natureza.codigo</c>. Os valores
    /// categóricos enviados substituem em bloco os gravados: o que não vier é removido.
    /// </summary>
    [HttpPut("admin/tipos-conceito/{id:guid}")]
    [EndpointName("AlterarTipoDeConceito")]
    [ExigePermissao<PermissaoConfiguracao>(PermissaoConfiguracao.TiposConceitoManter, ResourceTipo = ResourceTiposConfiguracao.TipoConceito)]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Atualizar(Guid id, [FromBody] AtualizarTipoConceitoCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (this.ConferirIdDaRota(id, command.Id, _mapper) is { } divergente)
        {
            return divergente;
        }
        Result resultado = await _commandBus.Send(command, cancellationToken).ConfigureAwait(false);
        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }

    [HttpDelete("admin/tipos-conceito/{id:guid}")]
    [EndpointName("DesativarTipoDeConceito")]
    [ExigePermissao<PermissaoConfiguracao>(PermissaoConfiguracao.TiposConceitoManter, ResourceTipo = ResourceTiposConfiguracao.TipoConceito)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Desativar(Guid id, CancellationToken cancellationToken)
    {
        Result resultado = await _commandBus.Send(new DesativarTipoConceitoCommand(id), cancellationToken).ConfigureAwait(false);
        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }
}
