namespace Unifesspa.UniPlus.OrganizacaoInstitucional.API.Controllers;

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
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Unidades;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.DTOs;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Queries.Unidades;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;

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
    private const string ResourceTag = "unidades";

    /// <summary>Limite defensivo de comprimento do termo de busca (cobre o Nome máximo + margem).</summary>
    private const int BuscaMaxLength = 256;

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
    /// Lista as unidades organizacionais ativas, paginadas por cursor opaco
    /// (ADR-0026). Navegação via header <c>Link</c> (RFC 5988/8288); cada item
    /// carrega seu <c>_links.self</c> (ADR-0029 §"Coleção"). Aceita filtros
    /// opcionais (issue #640): <c>q</c> (busca textual sobre sigla, nome,
    /// código, slug e alias, acento/caixa-insensível) e <c>tipo</c> (um ou mais
    /// valores de <see cref="TipoUnidade"/>, ex.: <c>?tipo=3&amp;tipo=4</c>).
    /// Os filtros viajam como query params e combinam com o cursor — o cliente
    /// reanexa-os a cada página ao seguir o <c>cursor</c> do header <c>Link</c>.
    /// </summary>
    [HttpGet("unidades")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "unidade", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<UnidadeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Listar(
        [FromCursor(ResourceTag)] PageRequest page,
        [FromQuery(Name = "q")] string? q,
        [FromQuery(Name = "tipo")] int[]? tipo,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);

        if (q is { Length: > BuscaMaxLength })
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Termo de busca muito longo",
                Detail = $"O parâmetro 'q' não pode exceder {BuscaMaxLength} caracteres.",
                Status = StatusCodes.Status400BadRequest,
            });
        }

        if (!TentarMapearTipos(tipo, out IReadOnlyList<TipoUnidade> tipos, out IActionResult? erroTipo))
        {
            return erroTipo!;
        }

        ListarUnidadesAtivasResult resultado = await _queryBus
            .Send(new ListarUnidadesAtivasQuery(page.AfterId, page.Limit, q, tipos), cancellationToken)
            .ConfigureAwait(false);

        // HATEOAS Level 1 (ADR-0029 §"Coleção"): cada item carrega seu _links.self.
        UnidadeDto[] comLinks = [.. resultado.Items.Select(u => u with { Links = _linksBuilder.Build(u) })];

        return await this.OkPaginatedAsync(
            comLinks, resultado.ProximoAfterId, page, ResourceTag,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Converte os valores numéricos do query param <c>tipo</c> em
    /// <see cref="TipoUnidade"/>, rejeitando (400) qualquer valor que não
    /// corresponda a um tipo definido. Deduplica e preserva a ordem. Ausência
    /// de <c>tipo</c> resulta em lista vazia (sem filtro).
    /// </summary>
    private bool TentarMapearTipos(
        int[]? valores,
        out IReadOnlyList<TipoUnidade> tipos,
        out IActionResult? erro)
    {
        if (valores is null || valores.Length == 0)
        {
            tipos = [];
            erro = null;
            return true;
        }

        List<TipoUnidade> mapeados = new(valores.Length);
        foreach (int valor in valores)
        {
            var candidato = (TipoUnidade)valor;
            if (!Enum.IsDefined(candidato) || candidato == TipoUnidade.Nenhum)
            {
                tipos = [];
                erro = BadRequest(new ProblemDetails
                {
                    Title = "Filtro de tipo inválido",
                    Detail = $"O valor de tipo '{valor}' não corresponde a um TipoUnidade válido.",
                    Status = StatusCodes.Status400BadRequest,
                });
                return false;
            }

            if (!mapeados.Contains(candidato))
            {
                mapeados.Add(candidato);
            }
        }

        tipos = mapeados;
        erro = null;
        return true;
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
