namespace Unifesspa.UniPlus.Publicacoes.API.Controllers;

using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Infrastructure.Core.Hateoas;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Application.Commands.TiposAtoPublicado;
using Unifesspa.UniPlus.Publicacoes.Application.DTOs;
using Unifesspa.UniPlus.Publicacoes.Application.Queries.TiposAtoPublicado;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;

/// <summary>
/// Endpoints públicos de leitura (<c>GET /api/publicacoes/tipos-ato</c>) e
/// endpoints admin (<c>POST/PUT/DELETE /api/publicacoes/admin/tipos-ato</c>)
/// restritos a <c>plataforma-admin</c>.
/// </summary>
/// <remarks>
/// A leitura é pública porque o significado de um código de tipo de ato numa data
/// é informação de ato publicado — dado institucional, sem PII. A autoria
/// (<c>created_by</c>) não é exposta.
/// </remarks>
[ApiController]
[Route("api/publicacoes")]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public.")]
public sealed class TiposAtoController : ControllerBase
{
    private const string ResourceTag = "tipos-ato";

    /// <summary>Mesmo formato exigido pelo agregado: caixa alta, palavras separadas por underscore.</summary>
    private const string CodigoPattern = "^[A-Z]+(_[A-Z]+)*$";

    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;
    private readonly IResourceLinksBuilder<TipoAtoPublicadoDto> _linksBuilder;

    public TiposAtoController(
        ICommandBus commandBus,
        IQueryBus queryBus,
        IDomainErrorMapper mapper,
        IResourceLinksBuilder<TipoAtoPublicadoDto> linksBuilder)
    {
        _commandBus = commandBus;
        _queryBus = queryBus;
        _mapper = mapper;
        _linksBuilder = linksBuilder;
    }

    /// <summary>
    /// Lista tipos de ato, paginados por cursor opaco bidirecional
    /// (ADR-0026 + ADR-0089). Navegação via header <c>Link</c>; cada item carrega
    /// seu <c>_links.self</c> (ADR-0029). Traz apenas as versões vigentes hoje
    /// (<c>vigentes=true</c> por default).
    /// </summary>
    /// <remarks>
    /// Uma versão com vigência futura é planejamento normativo ainda não anunciado.
    /// O default protege a listagem pública de divulgá-la antes do ato que a
    /// institui; <c>vigentes=false</c> devolve a série histórica completa.
    /// </remarks>
    [HttpGet("tipos-ato")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "tipo-ato", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<TipoAtoPublicadoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Listar(
        [FromCursor(ResourceTag)] PageRequest page,
        CancellationToken cancellationToken,
        [FromQuery] bool vigentes = true)
    {
        ArgumentNullException.ThrowIfNull(page);

        ListarTiposAtoPublicadoResult resultado = await _queryBus
            .Send(new ListarTiposAtoPublicadoQuery(page.AfterId, page.Limit, page.Direction, vigentes), cancellationToken)
            .ConfigureAwait(false);

        TipoAtoPublicadoDto[] comLinks =
            [.. resultado.Items.Select(t => t with { Links = _linksBuilder.Build(t) })];

        return await this.OkPaginatedAsync(
            comLinks, resultado.AnteriorAfterId, resultado.ProximoAfterId, page, ResourceTag,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Obtém uma versão de tipo de ato pelo Id. Retorna 404 quando inexistente.</summary>
    [HttpGet("tipos-ato/{id:guid}")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "tipo-ato", Versions = [1])]
    [ProducesResponseType(typeof(TipoAtoPublicadoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken cancellationToken)
    {
        TipoAtoPublicadoDto? tipo = await _queryBus
            .Send(new ObterTipoAtoPublicadoPorIdQuery(id), cancellationToken)
            .ConfigureAwait(false);

        if (tipo is null)
        {
            return NotFound();
        }

        return Ok(tipo with { Links = _linksBuilder.Build(tipo) });
    }

    /// <summary>
    /// Resolve o que um código de tipo de ato significava numa data: a única versão
    /// viva cuja janela semiaberta <c>[inicio, fim)</c> contém a data. Sem
    /// <c>data</c>, hoje.
    /// </summary>
    /// <remarks>
    /// O formato do código é validado aqui, no boundary. Sem isso, <c>edital_abertura</c>
    /// devolveria 404 ("não existe") em vez de 400 ("não é um código") — a resposta
    /// errada para o cliente, que concluiria que o tipo foi removido.
    /// </remarks>
    [HttpGet("tipos-ato/{codigo}/vigente")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "tipo-ato", Versions = [1])]
    [ProducesResponseType(typeof(TipoAtoPublicadoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ObterVigente(
        [FromRoute]
        [StringLength(60, MinimumLength = 1)]
        [RegularExpression(CodigoPattern, ErrorMessage = "Código do tipo de ato deve usar apenas letras maiúsculas sem acento, separadas por underscore (ex.: EDITAL_ABERTURA).")]
        string codigo,
        [FromQuery] DateOnly? data,
        CancellationToken cancellationToken)
    {
        TipoAtoPublicadoDto? tipo = await _queryBus
            .Send(new ObterTipoAtoPublicadoVigenteQuery(codigo, data), cancellationToken)
            .ConfigureAwait(false);

        if (tipo is null)
        {
            return NotFound();
        }

        return Ok(tipo with { Links = _linksBuilder.Build(tipo) });
    }

    /// <summary>
    /// Cria uma versão de tipo de ato. Restrito a <c>plataforma-admin</c>.
    /// <c>Idempotency-Key</c> obrigatório (ADR-0027).
    /// </summary>
    [HttpPost("admin/tipos-ato")]
    [Authorize(Roles = "plataforma-admin")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Criar(
        [FromBody] CriarTipoAtoPublicadoCommand command,
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

    /// <summary>
    /// Atualiza uma versão de tipo de ato. Restrito a <c>plataforma-admin</c>.
    /// </summary>
    /// <remarks>
    /// Sem <c>Idempotency-Key</c>: a ADR-0027 exclui <c>PUT</c> puro, cuja semântica
    /// já é idempotente — repetir a mesma substituição não acumula efeito.
    /// </remarks>
    [HttpPut("admin/tipos-ato/{id:guid}")]
    [Authorize(Roles = "plataforma-admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Atualizar(
        Guid id,
        [FromBody] AtualizarTipoAtoPublicadoCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (id != command.Id)
        {
            // Pelo mapeador, e não por um ProblemDetails montado à mão: assim o erro
            // sai com type, instance e traceId, como todo o resto do contrato.
            return Result.Failure(new DomainError(
                TipoAtoPublicadoErrorCodes.IdDivergente,
                "O Id na URL não corresponde ao Id no corpo da requisição."))
                .ToActionResult(_mapper);
        }

        Result resultado = await _commandBus.Send(command, cancellationToken).ConfigureAwait(false);

        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Remove (soft-delete) uma versão de tipo de ato, liberando a sua janela de
    /// vigência. Restrito a <c>plataforma-admin</c>.
    /// </summary>
    [HttpDelete("admin/tipos-ato/{id:guid}")]
    [Authorize(Roles = "plataforma-admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remover(Guid id, CancellationToken cancellationToken)
    {
        Result resultado = await _commandBus
            .Send(new RemoverTipoAtoPublicadoCommand(id), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }
}
