namespace Unifesspa.UniPlus.OrganizacaoInstitucional.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Infrastructure.Core.Authorization;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Infrastructure.Core.Hateoas;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Unidades;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.DTOs;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Permissoes;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Queries.Unidades;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;

/// <summary>
/// Endpoints públicos de leitura (<c>GET /api/organizacao/unidades</c>,
/// <c>GET /api/organizacao/unidades/{id}</c>) e endpoints admin
/// (<c>POST/PUT/DELETE /api/organizacao/admin/unidades</c>) restritos à
/// permissão de manutenção.
/// </summary>
/// <remarks>
/// O prefixo da rota é aplicado automaticamente por convenção.
/// e cada action especifica seu caminho.
/// </remarks>
[ApiController]
[Authorize]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public.")]
public sealed class UnidadesController : ControllerBase
{
    private const string ResourceTag = "unidades";

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
    [EndpointName("ListarUnidadesInstitucionais")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "unidade", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<UnidadeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, Description = "Valor de tipo que não é um tipo de unidade filtrável (uniplus.organizacao.unidade.filtro_de_tipo_invalido).")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity, Description = "O texto pesquisado excede 200 caracteres (uniplus.consulta.busca_muito_longa).")]
    public async Task<IActionResult> Listar(
        [FromCursor(ResourceTag)] PageRequest page,
        [FromQuery(Name = "q")] string? q,
        [FromQuery(Name = "tipo")] int[]? tipo,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);

        Result<ListarUnidadesAtivasResult> resultado = await _queryBus
            .Send(new ListarUnidadesAtivasQuery(page.AfterId, page.Limit, page.Direction, q, tipo), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess
            ? await RespostaPaginadaAsync(resultado.Value!, page, cancellationToken).ConfigureAwait(false)
            : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// A página de Unidades: cada item carrega seu <c>_links.self</c> (HATEOAS Level 1, ADR-0029
    /// §"Coleção") e a navegação segue por cursor no header <c>Link</c>.
    /// </summary>
    private async Task<IActionResult> RespostaPaginadaAsync(
        ListarUnidadesAtivasResult resultado, PageRequest page, CancellationToken cancellationToken)
    {
        UnidadeDto[] comLinks = [.. resultado.Items.Select(unidade => unidade with { Links = _linksBuilder.Build(unidade) })];

        return await this.OkPaginatedAsync(
            comLinks, resultado.AnteriorAfterId, resultado.ProximoAfterId, page, ResourceTag,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Obtém uma unidade pelo Id. Retorna 404 quando inexistente.
    /// </summary>
    [HttpGet("unidades/{id:guid}")]
    [EndpointName("ObterUnidadeInstitucional")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "unidade", Versions = [1])]
    [ProducesResponseType(typeof(UnidadeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, Description = "A Unidade não existe ou foi removida (uniplus.organizacao.unidade.nao_encontrada).")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken cancellationToken)
    {
        Result<UnidadeDto> consulta = await _queryBus
            .Send(new ObterUnidadePorIdQuery(id), cancellationToken)
            .ConfigureAwait(false);

        if (consulta.IsFailure)
        {
            return consulta.ToActionResult(_mapper);
        }

        UnidadeDto unidade = consulta.Value!;
        return Ok(unidade with { Links = _linksBuilder.Build(unidade) });
    }

    /// <summary>
    /// Cria uma nova unidade organizacional. Exige a permissão de manutenção.
    /// Idempotency-Key obrigatório (ADR-0027).
    /// </summary>
    [HttpPost("admin/unidades")]
    [EndpointName("CadastrarUnidadeInstitucional")]
    [ExigePermissao<PermissaoOrganizacao>(PermissaoOrganizacao.UnidadesManter, ResourceTipo = ResourceTiposOrganizacao.Unidade)]
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

        return RespostaDaCriacao(resultado);
    }

    private IActionResult RespostaDaCriacao(Result<Guid> resultado) =>
        resultado.IsSuccess
            ? CreatedAtAction(
                nameof(ObterPorId),
                new { id = resultado.Value },
                resultado.Value)
            : resultado.ToActionResult(_mapper);

    /// <summary>
    /// Atualiza uma unidade organizacional existente. Exige a permissão de
    /// manutenção.
    /// </summary>
    [HttpPut("admin/unidades/{id:guid}")]
    [EndpointName("AlterarUnidadeInstitucional")]
    [ExigePermissao<PermissaoOrganizacao>(PermissaoOrganizacao.UnidadesManter, ResourceTipo = ResourceTiposOrganizacao.Unidade)]
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

        if (this.ConferirIdDaRota(id, command.Id, _mapper) is { } divergente)
        {
            return divergente;
        }

        Result resultado = await _commandBus
            .Send(command, cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Remove (soft-delete) uma unidade organizacional. Exige a permissão de
    /// manutenção.
    /// </summary>
    [HttpDelete("admin/unidades/{id:guid}")]
    [EndpointName("ExcluirUnidadeInstitucional")]
    [ExigePermissao<PermissaoOrganizacao>(PermissaoOrganizacao.UnidadesManter, ResourceTipo = ResourceTiposOrganizacao.Unidade)]
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
