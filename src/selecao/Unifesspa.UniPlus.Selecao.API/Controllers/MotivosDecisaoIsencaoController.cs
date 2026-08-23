namespace Unifesspa.UniPlus.Selecao.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Authorization;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Infrastructure.Core.Hateoas;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.MotivosDecisaoIsencao;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.MotivosDecisaoIsencao;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Catálogo institucional de motivos de decisão de isenção (UNI-REQ-0120).
/// Leitura pública sob <c>/api/selecao/motivos-decisao-isencao</c>; manutenção
/// sob <c>/api/selecao/admin/motivos-decisao-isencao</c> (ADR-0064).
/// </summary>
/// <remarks>
/// <para>
/// <b>A autorização é por permissão, não por nome de perfil.</b> Toda escrita
/// passa pelo ponto de decisão único (ADR-0078) exigindo
/// <c>configuracao:motivos-decisao-recursal:manter</c>. Qualquer perfil
/// institucional pode recebê-la sem mudança de código, e participar de banca
/// não a concede.
/// </para>
/// <para>
/// O <c>[Authorize]</c> sem papel na classe é o que separa os dois desfechos:
/// quem não se autenticou recebe <c>401</c> da própria pipeline, antes de
/// qualquer decisão; quem se autenticou e não tem a concessão recebe
/// <c>403</c> da decisão. Colapsar os dois faria o não-identificado parecer
/// não-autorizado.
/// </para>
/// </remarks>
[ApiController]
[Authorize]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public.")]
public sealed class MotivosDecisaoIsencaoController : ControllerBase
{
    private const string ResourceTag = "motivos-decisao-isencao";
    private const string RecursoTipo = "MotivoDecisaoIsencao";

    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;
    private readonly IResourceLinksBuilder<MotivoDecisaoIsencaoDto> _linksBuilder;
    private readonly IVerificadorDeAcesso _acesso;

    public MotivosDecisaoIsencaoController(
        ICommandBus commandBus,
        IQueryBus queryBus,
        IDomainErrorMapper mapper,
        IResourceLinksBuilder<MotivoDecisaoIsencaoDto> linksBuilder,
        IVerificadorDeAcesso acesso)
    {
        _commandBus = commandBus;
        _queryBus = queryBus;
        _mapper = mapper;
        _linksBuilder = linksBuilder;
        _acesso = acesso;
    }

    /// <summary>
    /// Lista o catálogo, paginado por cursor opaco bidirecional (ADR-0026 +
    /// ADR-0089). Por padrão devolve só os motivos ativos — a visão de quem
    /// monta uma publicação.
    /// </summary>
    [HttpGet("motivos-decisao-isencao")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "motivo-decisao-isencao", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<MotivoDecisaoIsencaoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Listar(
        [FromCursor(ResourceTag)] PageRequest page,
        [FromQuery] FundamentoIsencao? fundamento,
        [FromQuery] bool apenasAtivos = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);

        ListarMotivosDecisaoIsencaoResult resultado = await _queryBus.Send(
            new ListarMotivosDecisaoIsencaoQuery(
                page.AfterId,
                page.Limit,
                page.Direction,
                fundamento,
                apenasAtivos),
            cancellationToken).ConfigureAwait(false);

        MotivoDecisaoIsencaoDto[] comLinks =
            [.. resultado.Items.Select(m => m with { Links = _linksBuilder.Build(m) })];

        return await this.OkPaginatedAsync(
            comLinks,
            resultado.AnteriorAfterId,
            resultado.ProximoAfterId,
            page,
            ResourceTag,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Obtém um motivo pelo Id. Retorna 404 quando inexistente.</summary>
    [HttpGet("motivos-decisao-isencao/{id:guid}")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "motivo-decisao-isencao", Versions = [1])]
    [ProducesResponseType(typeof(MotivoDecisaoIsencaoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken cancellationToken)
    {
        MotivoDecisaoIsencaoDto? motivo = await _queryBus
            .Send(new ObterMotivoDecisaoIsencaoQuery(id), cancellationToken)
            .ConfigureAwait(false);

        if (motivo is null)
        {
            return NotFound();
        }

        return Ok(motivo with { Links = _linksBuilder.Build(motivo) });
    }

    /// <summary>
    /// Cria um motivo no catálogo. Exige a permissão de manutenção.
    /// <c>Idempotency-Key</c> obrigatório (ADR-0027).
    /// </summary>
    [HttpPost("admin/motivos-decisao-isencao")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Criar(
        [FromBody] CriarMotivoDecisaoIsencaoCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (await NegarSemPermissaoDeManutencao(cancellationToken).ConfigureAwait(false) is { } negativa)
        {
            return negativa;
        }

        Result<Guid> resultado = await _commandBus.Send(command, cancellationToken).ConfigureAwait(false);

        return resultado.IsSuccess
            ? CreatedAtAction(nameof(ObterPorId), new { id = resultado.Value }, resultado.Value)
            : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Edita a descrição do motivo. Código, fundamento e resultado permitido
    /// não são editáveis (UNI-REQ-0121). <c>Idempotency-Key</c> obrigatório.
    /// </summary>
    [HttpPut("admin/motivos-decisao-isencao/{id:guid}")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Atualizar(
        Guid id,
        [FromBody] AtualizarMotivoDecisaoIsencaoCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (await NegarSemPermissaoDeManutencao(cancellationToken).ConfigureAwait(false) is { } negativa)
        {
            return negativa;
        }

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
    [HttpPost("admin/motivos-decisao-isencao/{id:guid}/ativacao")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Ativar(Guid id, CancellationToken cancellationToken)
    {
        if (await NegarSemPermissaoDeManutencao(cancellationToken).ConfigureAwait(false) is { } negativa)
        {
            return negativa;
        }

        Result resultado = await _commandBus
            .Send(new AtivarMotivoDecisaoIsencaoCommand(id), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Desativa um motivo. O efeito é prospectivo (UNI-REQ-0122): ele deixa de
    /// entrar em novas publicações e permanece onde já foi disponibilizado,
    /// inclusive nas decisões já proferidas.
    /// </summary>
    [HttpDelete("admin/motivos-decisao-isencao/{id:guid}/ativacao")]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Desativar(Guid id, CancellationToken cancellationToken)
    {
        if (await NegarSemPermissaoDeManutencao(cancellationToken).ConfigureAwait(false) is { } negativa)
        {
            return negativa;
        }

        Result resultado = await _commandBus
            .Send(new DesativarMotivoDecisaoIsencaoCommand(id), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Devolve a resposta de recusa quando o solicitante não pode manter o
    /// catálogo, e <see langword="null"/> quando pode seguir.
    /// </summary>
    /// <remarks>
    /// A decisão distingue "não pôde ser identificado" de "não tem a
    /// concessão": o primeiro é <c>401</c>, e responder <c>403</c> a ele diria
    /// que a identidade foi lida e recusada, quando não foi.
    /// </remarks>
    private async Task<IActionResult?> NegarSemPermissaoDeManutencao(CancellationToken cancellationToken)
    {
        ResultadoDoAcesso resultado = await _acesso.VerificarAsync(
            UniPlusPermissions.ConfiguracaoMotivosDecisaoRecursalManterRequirement,
            ResourceContext.From(RecursoTipo, Sensibilidade.Interna).Value!,
            cancellationToken).ConfigureAwait(false);

        return resultado switch
        {
            ResultadoDoAcesso.Permitido => null,
            ResultadoDoAcesso.IdentidadeIncompleta => Unauthorized(),
            _ => Forbid(),
        };
    }
}
