namespace Unifesspa.UniPlus.Autorizacao.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Termos;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Permissoes;
using Unifesspa.UniPlus.Autorizacao.Application.Queries.Termos;
using Unifesspa.UniPlus.Infrastructure.Core.Authorization;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O termo de responsabilidade sob <c>/api/autorizacao/termos-de-responsabilidade</c>: o gestor de acessos publica versões
/// imutáveis, e quem pede acesso a dado pessoal ou sensível lê a vigente; o beneficiário lê a versão fixada no pedido.
/// </summary>
/// <remarks>
/// O texto, o ato e o resumo são institucionais; o autor da publicação fica gravado para auditoria e não é exposto, de
/// modo que as consultas não tratam dado pessoal (ADR-0162).
/// </remarks>
[ApiController]
[Authorize]
public sealed class TermosDeResponsabilidadeController : ControllerBase
{
    private const string ResourceTag = "versoes-do-termo";

    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;

    public TermosDeResponsabilidadeController(ICommandBus commandBus, IQueryBus queryBus, IDomainErrorMapper mapper)
    {
        _commandBus = commandBus;
        _queryBus = queryBus;
        _mapper = mapper;
    }

    /// <summary>
    /// Publica uma versão nova, que passa a ser a vigente. Informe a vigente que a tela carregou: se outra foi publicada
    /// depois, a resposta é 409 e nada é gravado. O texto igual ao da vigente é recusado. O resumo devolvido é o SHA-256 do
    /// texto devolvido.
    /// </summary>
    [HttpPost("termos-de-responsabilidade/versoes")]
    [RequiresIdempotencyKey]
    [ExigePermissao<PermissaoAutorizacao>(PermissaoAutorizacao.TermosPublicar, ResourceTipo = ResourceTiposAutorizacao.TermoDeResponsabilidade)]
    [ProducesResponseType(typeof(VersaoDoTermoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Publicar([FromBody] PublicarVersaoDoTermoRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<VersaoDoTermoDto> resultado = await _commandBus
            .Send(new PublicarVersaoDoTermoCommand(request.Texto, request.Ato, request.VersaoVigenteEsperadaId), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess
            ? CreatedAtAction(nameof(ObterVersao), new { id = resultado.Value!.Id }, resultado.Value)
            : resultado.ToActionResult(_mapper);
    }

    /// <summary>A versão vigente, para quem vai pedir acesso; 404 se nenhuma foi publicada.</summary>
    [HttpGet("termos-de-responsabilidade/vigente")]
    [VendorMediaType(Resource = "versao-do-termo", Versions = [1])]
    [ProducesResponseType(typeof(VersaoDoTermoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ObterVigente(CancellationToken cancellationToken) =>
        Resposta(await _queryBus.Send(new ObterTermoVigenteQuery(), cancellationToken).ConfigureAwait(false));

    /// <summary>Uma versão pelo id, vigente ou não: a tela de aceite mostra a versão fixada no pedido.</summary>
    [HttpGet("termos-de-responsabilidade/versoes/{id:guid}")]
    [VendorMediaType(Resource = "versao-do-termo", Versions = [1])]
    [ProducesResponseType(typeof(VersaoDoTermoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> ObterVersao(Guid id, CancellationToken cancellationToken) =>
        Resposta(await _queryBus.Send(new ObterVersaoDoTermoQuery(id), cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// O histórico das versões, sem o texto, em ordem de publicação, paginado por cursor opaco bidirecional (ADR-0026 +
    /// ADR-0089). Só para quem publica o termo.
    /// </summary>
    [HttpGet("termos-de-responsabilidade/versoes")]
    [ExigePermissao<PermissaoAutorizacao>(PermissaoAutorizacao.TermosPublicar, ResourceTipo = ResourceTiposAutorizacao.TermoDeResponsabilidade)]
    [VendorMediaType(Resource = "versao-do-termo", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<VersaoDoTermoResumoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    public async Task<IActionResult> ListarVersoes([FromCursor(ResourceTag)] PageRequest page, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);

        Result<ListarVersoesDoTermoResult> resultado = await _queryBus
            .Send(new ListarVersoesDoTermoQuery(page.AfterId, page.Limit, page.Direction), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsFailure
            ? resultado.ToActionResult(_mapper)
            : await this.OkPaginatedAsync(
                resultado.Value!.Items, resultado.Value.AnteriorAfterId, resultado.Value.ProximoAfterId, page, ResourceTag,
                cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private IActionResult Resposta<T>(Result<T> resultado) =>
        resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
}
