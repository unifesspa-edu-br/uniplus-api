namespace Unifesspa.UniPlus.Autorizacao.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Permissoes;
using Unifesspa.UniPlus.Autorizacao.Application.Queries.Perfis;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Authorization;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Perfis de acesso sob <c>/api/autorizacao/perfis</c>.
/// </summary>
/// <remarks>
/// As operações de manutenção exigem <c>autorizacao:perfis:manter</c> pelo ponto de decisão único; a leitura
/// aceita também <c>autorizacao:perfis:aprovar-protecao-de-dados</c> e <c>autorizacao:perfis:aprovar-funcional</c>, e o
/// handler a decide antes de ler qualquer dado. O <c>[Authorize]</c> sem papel na classe separa os desfechos: quem não se
/// autenticou recebe <c>401</c> da própria pipeline; quem se autenticou sem a concessão recebe <c>403</c>.
/// </remarks>
[ApiController]
[Authorize]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public.")]
public sealed class PerfisController : ControllerBase
{
    private const string ResourceTag = "perfis";

    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;

    public PerfisController(ICommandBus commandBus, IQueryBus queryBus, IDomainErrorMapper mapper)
    {
        _commandBus = commandBus;
        _queryBus = queryBus;
        _mapper = mapper;
    }

    /// <summary>Lista os perfis, paginados por cursor opaco bidirecional (ADR-0026 + ADR-0089).</summary>
    [HttpGet("perfis")]
    [VendorMediaType(Resource = "perfil", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<PerfilListadoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Listar(
        [FromCursor(ResourceTag)] PageRequest page,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);

        Result<ListarPerfisResult> resultado = await _queryBus.Send(
            new ListarPerfisQuery(page.AfterId, page.Limit, page.Direction),
            cancellationToken).ConfigureAwait(false);

        return resultado.IsFailure
            ? resultado.ToActionResult(_mapper)
            : await PaginaAsync(resultado.Value!, page, cancellationToken).ConfigureAwait(false);
    }

    private Task<IActionResult> PaginaAsync(ListarPerfisResult pagina, PageRequest page, CancellationToken cancellationToken) =>
        this.OkPaginatedAsync(pagina.Items, pagina.AnteriorAfterId, pagina.ProximoAfterId, page, ResourceTag, cancellationToken: cancellationToken);

    /// <summary>Obtém um perfil pelo Id. Retorna 404 quando inexistente.</summary>
    [HttpGet("perfis/{id:guid}")]
    [VendorMediaType(Resource = "perfil", Versions = [1])]
    [ProducesResponseType(typeof(PerfilDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken cancellationToken)
    {
        Result<PerfilDto> resultado = await _queryBus
            .Send(new ObterPerfilQuery(id), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Cria o perfil com a versão 1 pendente e devolve a URL de PUT do documento que a institui. O perfil só entra em
    /// vigor quando esse documento é verificado. <c>Idempotency-Key</c> obrigatório (ADR-0027).
    /// </summary>
    [HttpPost("perfis")]
    [ExigePermissao<PermissaoAutorizacao>(PermissaoAutorizacao.PerfisManter, ResourceTipo = ResourceTiposAutorizacao.Perfil)]
    [RequiresIdempotencyKey(TtlSeconds = 15 * 60)]
    [ProducesResponseType(typeof(EnvioDoDocumentoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Criar(
        [FromBody] DadosDaVersaoRequest dados,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dados);

        Result<EnvioDoDocumentoDto> resultado = await _commandBus
            .Send(new CriarPerfilCommand(dados), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess
            ? CreatedAtAction(nameof(ObterPorId), new { id = resultado.Value!.PerfilId }, resultado.Value)
            : resultado.ToActionResult(_mapper);
    }
}
