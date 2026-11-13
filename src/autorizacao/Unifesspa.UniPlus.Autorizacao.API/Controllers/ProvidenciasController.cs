namespace Unifesspa.UniPlus.Autorizacao.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Providencias;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Permissoes;
using Unifesspa.UniPlus.Autorizacao.Application.Queries.Providencias;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Infrastructure.Core.Authorization;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Providências sobre concessões sob <c>/api/autorizacao/providencias</c> (UNI-REQ-0375, UNI-REQ-0376, ADR-0164): o
/// Encarregado de dados solicita, com fundamento, a retirada ou a revisão de uma concessão, e quem administra concessões,
/// os gestores de acessos e os Representantes do controlador, responde com motivo ou atende pela revogação vinculada.
/// </summary>
/// <remarks>
/// Solicitar exige <c>autorizacao:providencias:solicitar</c>; responder e atender, <c>autorizacao:concessoes:administrar</c>,
/// a mesma que revoga; ler, uma das duas. Toda escrita exige <c>Idempotency-Key</c>, e a repetição com a mesma chave
/// devolve a mesma resposta. Cada providência exibida é registrada antes da resposta; sem o registro, <c>503</c>.
/// </remarks>
[ApiController]
[Authorize]
public sealed class ProvidenciasController : ControllerBase
{
    private const string ResourceTag = "providencias";

    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;

    public ProvidenciasController(ICommandBus commandBus, IQueryBus queryBus, IDomainErrorMapper mapper)
    {
        _commandBus = commandBus;
        _queryBus = queryBus;
        _mapper = mapper;
    }

    /// <summary>
    /// Solicita providência sobre uma concessão ativa: <c>retirada</c> ou <c>revisao</c>, com fundamento de 10 a 2000
    /// caracteres. Há no máximo uma providência aberta por concessão. Os gestores de acessos e os Representantes do
    /// controlador são avisados por e-mail, sem o fundamento nem nome de pessoa.
    /// </summary>
    [HttpPost("providencias")]
    [RequiresIdempotencyKey]
    [ExigePermissao<PermissaoAutorizacao>(PermissaoAutorizacao.ProvidenciasSolicitar, ResourceTipo = ResourceTiposAutorizacao.Providencia)]
    [ProducesResponseType(typeof(ProvidenciaDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Solicitar([FromBody] SolicitarProvidenciaRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<ProvidenciaDto> resultado = await _commandBus
            .Send(new SolicitarProvidenciaCommand(request.ConcessaoId, new SolicitacaoDaProvidencia(request.Tipo, request.Fundamento)), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess
            ? CreatedAtAction(nameof(Obter), new { id = resultado.Value!.Id }, resultado.Value)
            : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Responde a providência com motivo, sem revogar. Exige providência aberta e concessão ativa no instante da gravação;
    /// quem solicitou e o beneficiário da concessão não respondem. Quem solicitou é avisado por e-mail.
    /// </summary>
    [HttpPost("providencias/{id:guid}/resposta")]
    [RequiresIdempotencyKey]
    [ExigePermissao<PermissaoAutorizacao>(PermissaoAutorizacao.ConcessoesAdministrar, ResourceTipo = ResourceTiposAutorizacao.Providencia)]
    [ProducesResponseType(typeof(ProvidenciaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Responder(Guid id, [FromBody] ResponderProvidenciaRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Resposta(await _commandBus.Send(new ResponderProvidenciaCommand(id, request.Motivo), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Atende a providência pela revogação vinculada, que segue a revogação comum: o e-mail do beneficiário confirma a
    /// pessoa, e a concessão de gestor de acessos não se revoga se deixaria menos de dois gestores. Quem solicitou e o
    /// beneficiário são avisados por e-mail.
    /// </summary>
    [HttpPost("providencias/{id:guid}/atendimento")]
    [RequiresIdempotencyKey]
    [ExigePermissao<PermissaoAutorizacao>(PermissaoAutorizacao.ConcessoesAdministrar, ResourceTipo = ResourceTiposAutorizacao.Providencia)]
    [ProducesResponseType(typeof(ProvidenciaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Atender(Guid id, [FromBody] AtenderProvidenciaRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        AtenderProvidenciaCommand command = new(id, new DeclaracaoDoAtendimento(request.Motivo, request.ConfirmacaoEmail));

        return Resposta(await _commandBus.Send(command, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Lê a providência, com o fundamento, a situação derivada no instante e o desfecho com o motivo.</summary>
    [HttpGet("providencias/{id:guid}")]
    [ExigePermissao<PermissaoAutorizacao>(
        PermissaoAutorizacao.ConcessoesAdministrar,
        ResourceTipo = ResourceTiposAutorizacao.Providencia,
        OuPermissao = PermissaoAutorizacao.ProvidenciasSolicitar)]
    [VendorMediaType(Resource = "providencia", Versions = [1])]
    [ProducesResponseType(typeof(ProvidenciaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Obter(Guid id, CancellationToken cancellationToken) =>
        Resposta(await _queryBus.Send(new ObterProvidenciaQuery(id), cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// A fila das providências, paginada por cursor opaco bidirecional (ADR-0026 + ADR-0089), em ordem de abertura, a mais
    /// antiga primeiro; no mesmo instante, pelo identificador. Sem
    /// <c>situacao</c>, a fila prioritária: as abertas com concessão ativa. <c>situacao</c> aceita <c>aberta</c>,
    /// <c>respondida</c>, <c>atendida</c>, <c>prejudicada</c> ou <c>todas</c>; <c>concessao</c> restringe às de uma
    /// concessão. Os itens não trazem o fundamento nem o motivo do desfecho.
    /// </summary>
    [HttpGet("providencias")]
    [ExigePermissao<PermissaoAutorizacao>(
        PermissaoAutorizacao.ConcessoesAdministrar,
        ResourceTipo = ResourceTiposAutorizacao.Providencia,
        OuPermissao = PermissaoAutorizacao.ProvidenciasSolicitar)]
    [VendorMediaType(Resource = "providencia", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<ProvidenciaResumoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Listar(
        [FromCursor(ResourceTag, RequireSortKey = true)] PageRequest page,
        [FromQuery] string? situacao = null,
        [FromQuery] Guid? concessao = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);

        if (Consulta(page, situacao, concessao) is not { } consulta)
        {
            return Result<ListarProvidenciasResult>.Failure(FiltroDeSituacaoInvalido()).ToActionResult(_mapper);
        }

        ListarProvidenciasResult resultado = await _queryBus.Send(consulta, cancellationToken).ConfigureAwait(false);

        return await this.OkPaginatedOrdenadoAsync(
            resultado.Items, resultado.Anterior, resultado.Proximo, page, ResourceTag, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private IActionResult Resposta(Result<ProvidenciaDto> resultado) =>
        resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);

    // Sem situação, a fila prioritária: as abertas com concessão ativa. "todas" tira o filtro; outro valor é recusado.
    private static ListarProvidenciasQuery? Consulta(PageRequest page, string? situacao, Guid? concessao) =>
        situacao?.Trim().ToLowerInvariant() switch
        {
            null or "" => new(SituacaoDaProvidencia.Aberta, concessao, page.AfterSortKey, page.AfterId, page.Limit, page.Direction),
            "todas" => new(null, concessao, page.AfterSortKey, page.AfterId, page.Limit, page.Direction),
            var codigo when SituacaoDaProvidenciaCodigo.DoCodigo(codigo) is { } lida => new(lida, concessao, page.AfterSortKey, page.AfterId, page.Limit, page.Direction),
            _ => null,
        };

    private static DomainError FiltroDeSituacaoInvalido() =>
        new(ProvidenciaErrorCodes.FiltroDeSituacaoInvalido, "Situação inválida. Use aberta, respondida, atendida, prejudicada ou todas.");
}
