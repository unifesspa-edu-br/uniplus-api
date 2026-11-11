namespace Unifesspa.UniPlus.Selecao.API.Controllers;

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
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Application.Queries.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Cadastro único de motivos de decisão, organizado por tipo (UNI-REQ-0283).
/// Leitura pública sob <c>/api/selecao/motivos-de-decisao</c>; manutenção
/// sob <c>/api/selecao/admin/motivos-de-decisao</c> (ADR-0064).
/// </summary>
/// <remarks>
/// <para>
/// <b>A autorização é por permissão, não por nome de perfil.</b> Toda escrita
/// passa pelo ponto de decisão único (ADR-0078) exigindo
/// <c>selecao:motivos-de-decisao:manter</c>. Qualquer perfil
/// institucional pode recebê-la sem mudança de código, e participar de banca
/// não a concede.
/// </para>
/// <para>
/// O <c>[Authorize]</c> sem papel na classe é o que separa os dois desfechos:
/// quem não se autenticou recebe <c>401</c> da própria pipeline, antes de
/// qualquer decisão; quem se autenticou e não tem a concessão recebe
/// <c>403</c> do <c>[ExigePermissao]</c> de cada ação de manutenção. Colapsar os dois faria o não-identificado parecer
/// não-autorizado.
/// </para>
/// </remarks>
[ApiController]
[Authorize]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public.")]
public sealed class MotivosDeDecisaoController : ControllerBase
{
    private const string ResourceTag = "motivos-de-decisao";

    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;
    private readonly IResourceLinksBuilder<MotivoDeDecisaoDto> _linksBuilder;

    public MotivosDeDecisaoController(
        ICommandBus commandBus,
        IQueryBus queryBus,
        IDomainErrorMapper mapper,
        IResourceLinksBuilder<MotivoDeDecisaoDto> linksBuilder)
    {
        _commandBus = commandBus;
        _queryBus = queryBus;
        _mapper = mapper;
        _linksBuilder = linksBuilder;
    }

    /// <summary>
    /// Lista o cadastro, paginado por cursor opaco bidirecional (ADR-0026 +
    /// ADR-0089), com filtro por tipo e subclassificação. Por padrão devolve só
    /// os motivos ativos — a visão de quem monta uma publicação.
    /// </summary>
    [HttpGet("motivos-de-decisao")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "motivo-de-decisao", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<MotivoDeDecisaoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Listar(
        [FromCursor(ResourceTag)] PageRequest page,
        [FromQuery] string? tipo,
        [FromQuery] string? subclassificacao,
        [FromQuery] bool apenasAtivos = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);

        if (!FiltroDaListagemDeMotivos.TentarLer(tipo, subclassificacao, apenasAtivos, out FiltroDosMotivos filtro, out ProblemDetails? recusa))
        {
            return BadRequest(recusa);
        }

        ListarMotivosDeDecisaoResult resultado = await _queryBus
            .Send(new ListarMotivosDeDecisaoQuery(page.AfterId, page.Limit, page.Direction, filtro), cancellationToken)
            .ConfigureAwait(false);

        MotivoDeDecisaoDto[] comLinks = [.. resultado.Items.Select(m => m with { Links = _linksBuilder.Build(m) })];
        return await this.OkPaginatedAsync(comLinks, resultado.AnteriorAfterId, resultado.ProximoAfterId, page, ResourceTag, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Obtém um motivo pelo Id. Retorna 404 quando inexistente.</summary>
    [HttpGet("motivos-de-decisao/{id:guid}")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "motivo-de-decisao", Versions = [1])]
    [ProducesResponseType(typeof(MotivoDeDecisaoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken cancellationToken)
    {
        MotivoDeDecisaoDto? motivo = await _queryBus
            .Send(new ObterMotivoDeDecisaoQuery(id), cancellationToken)
            .ConfigureAwait(false);

        if (motivo is null)
        {
            return NotFound();
        }

        return Ok(motivo with { Links = _linksBuilder.Build(motivo) });
    }

    /// <summary>
    /// Cria um motivo no cadastro. Exige a permissão de manutenção.
    /// <c>Idempotency-Key</c> obrigatório (ADR-0027).
    /// </summary>
    [HttpPost("admin/motivos-de-decisao")]
    [ExigePermissao<PermissaoSelecao>(PermissaoSelecao.MotivosDeDecisaoManter, ResourceTipo = ResourceTiposSelecao.MotivoDeDecisao)]
    [RequiresIdempotencyKey]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Criar(
        [FromBody] CriarMotivoDeDecisaoCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        Result<Guid> resultado = await _commandBus.Send(command, cancellationToken).ConfigureAwait(false);

        return resultado.IsSuccess
            ? CreatedAtAction(nameof(ObterPorId), new { id = resultado.Value }, resultado.Value)
            : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Edita a descrição do motivo. Tipo, código, subclassificação e
    /// sentido não são editáveis (UNI-REQ-0121, UNI-REQ-0283). <c>Idempotency-Key</c> obrigatório.
    /// </summary>
    [HttpPut("admin/motivos-de-decisao/{id:guid}")]
    [ExigePermissao<PermissaoSelecao>(PermissaoSelecao.MotivosDeDecisaoManter, ResourceTipo = ResourceTiposSelecao.MotivoDeDecisao)]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Atualizar(
        Guid id,
        [FromBody] AtualizarMotivoDeDecisaoCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.Id != id)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Id do path não corresponde ao Id do corpo",
                Status = StatusCodes.Status400BadRequest,
            });
        }

        Result resultado = await _commandBus.Send(command, cancellationToken).ConfigureAwait(false);

        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }

    /// <summary>Reativa um motivo, devolvendo-o às novas publicações.</summary>
    [HttpPost("admin/motivos-de-decisao/{id:guid}/ativacao")]
    [ExigePermissao<PermissaoSelecao>(PermissaoSelecao.MotivosDeDecisaoManter, ResourceTipo = ResourceTiposSelecao.MotivoDeDecisao)]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Reativar(Guid id, CancellationToken cancellationToken)
    {
        Result resultado = await _commandBus
            .Send(new ReativarMotivoDeDecisaoCommand(id), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Desativa um motivo. O efeito é prospectivo (UNI-REQ-0122): ele deixa de
    /// entrar em novas publicações e permanece onde já foi disponibilizado,
    /// inclusive nas decisões já proferidas.
    /// </summary>
    [HttpDelete("admin/motivos-de-decisao/{id:guid}/ativacao")]
    [ExigePermissao<PermissaoSelecao>(PermissaoSelecao.MotivosDeDecisaoManter, ResourceTipo = ResourceTiposSelecao.MotivoDeDecisao)]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Desativar(Guid id, CancellationToken cancellationToken)
    {
        Result resultado = await _commandBus
            .Send(new DesativarMotivoDeDecisaoCommand(id), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }
}
