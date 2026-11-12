namespace Unifesspa.UniPlus.Autorizacao.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Concessoes;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Permissoes;
using Unifesspa.UniPlus.Autorizacao.Application.Queries.Concessoes;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Infrastructure.Core.Authorization;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Concessões de acesso sob <c>/api/autorizacao/concessoes</c>: consulta, trilha e revogação.
/// </summary>
/// <remarks>
/// Não há rota de criação nem de edição: a concessão nasce do pedido aprovado e da instalação, e
/// não se edita. A leitura exige <c>autorizacao:concessoes:administrar</c> ou
/// <c>autorizacao:concessoes:consultar</c>, e a revogação só a primeira, pelo ponto de decisão
/// único: quem não se autenticou recebe <c>401</c> da própria pipeline; quem se autenticou sem a
/// concessão recebe <c>403</c> do <c>[ExigePermissao]</c>. Toda leitura é registrada antes da
/// resposta; sem o registro, é recusada com <c>503</c>.
/// </remarks>
[ApiController]
[Authorize]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public.")]
public sealed class ConcessoesController : ControllerBase
{
    private const string ResourceTag = "concessoes";

    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;

    public ConcessoesController(ICommandBus commandBus, IQueryBus queryBus, IDomainErrorMapper mapper)
    {
        _commandBus = commandBus;
        _queryBus = queryBus;
        _mapper = mapper;
    }

    /// <summary>
    /// Lista as concessões, paginadas por cursor opaco bidirecional (ADR-0026 + ADR-0089). A
    /// pessoa é o par <c>emissor</c> e <c>pessoa</c> (o <c>sub</c>), nunca CPF; um sem o outro é
    /// recusado.
    /// </summary>
    [HttpGet("concessoes")]
    [ExigePermissao<PermissaoAutorizacao>(
        PermissaoAutorizacao.ConcessoesAdministrar,
        ResourceTipo = ResourceTiposAutorizacao.Concessao,
        OuPermissao = PermissaoAutorizacao.ConcessoesConsultar)]
    [VendorMediaType(Resource = "concessao", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<ConcessaoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Listar(
        [FromCursor(ResourceTag)] PageRequest page,
        [FromQuery] string? pessoa = null,
        [FromQuery] string? emissor = null,
        [FromQuery] Guid? unidade = null,
        [FromQuery] Guid? perfil = null,
        [FromQuery] string? situacao = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);

        Result<ListarConcessoesQuery> consulta = Consulta(page, pessoa, emissor, unidade, perfil, situacao);
        if (consulta.IsFailure)
        {
            return consulta.ToActionResult(_mapper);
        }

        ListarConcessoesResult resultado = await _queryBus.Send(consulta.Value!, cancellationToken).ConfigureAwait(false);

        return await this.OkPaginatedAsync(
            resultado.Items, resultado.AnteriorAfterId, resultado.ProximoAfterId, page, ResourceTag, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Obtém uma concessão pelo Id. Retorna 404 quando inexistente.</summary>
    [HttpGet("concessoes/{id:guid}")]
    [ExigePermissao<PermissaoAutorizacao>(
        PermissaoAutorizacao.ConcessoesAdministrar,
        ResourceTipo = ResourceTiposAutorizacao.Concessao,
        OuPermissao = PermissaoAutorizacao.ConcessoesConsultar)]
    [VendorMediaType(Resource = "concessao", Versions = [1])]
    [ProducesResponseType(typeof(ConcessaoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken cancellationToken)
    {
        Result<ConcessaoDto> resultado = await _queryBus
            .Send(new ObterConcessaoQuery(id), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
    }

    /// <summary>Trilha da concessão: eventos em ordem cronológica, montados na leitura.</summary>
    [HttpGet("concessoes/{id:guid}/trilha")]
    [ExigePermissao<PermissaoAutorizacao>(
        PermissaoAutorizacao.ConcessoesAdministrar,
        ResourceTipo = ResourceTiposAutorizacao.Concessao,
        OuPermissao = PermissaoAutorizacao.ConcessoesConsultar)]
    [VendorMediaType(Resource = "trilha-da-concessao", Versions = [1])]
    [ProducesResponseType(typeof(IReadOnlyList<EventoDaTrilhaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Trilha(Guid id, CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<EventoDaTrilhaDto>> resultado = await _queryBus
            .Send(new ObterTrilhaDaConcessaoQuery(id), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Revoga a concessão, com efeito imediato e sem aprovação. A concessão de gestor de acessos
    /// não se revoga se deixaria menos de dois gestores. <c>Idempotency-Key</c> obrigatório.
    /// </summary>
    [HttpPost("concessoes/{id:guid}/revogacao")]
    [ExigePermissao<PermissaoAutorizacao>(PermissaoAutorizacao.ConcessoesAdministrar, ResourceTipo = ResourceTiposAutorizacao.Concessao)]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Revogar(
        Guid id,
        [FromBody] RevogarConcessaoRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result resultado = await _commandBus
            .Send(new RevogarConcessaoCommand(id, request.Motivo, request.ConfirmacaoEmail), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }

    // A pessoa é o par emissor e sub, nunca CPF, e um sem o outro é recusado; a situação é uma das quatro conhecidas.
    private static Result<ListarConcessoesQuery> Consulta(
        PageRequest page, string? pessoa, string? emissor, Guid? unidade, Guid? perfil, string? situacao)
    {
        (pessoa, emissor) = (Aparado(pessoa), Aparado(emissor));
        if ((pessoa is null) != (emissor is null))
        {
            return Recusa(ConcessaoErrorCodes.FiltroDePessoaInvalido, "A pessoa é identificada pelo par emissor e pessoa (sub); informe os dois.");
        }

        return LerSituacao(situacao) is { } lida
            ? Result<ListarConcessoesQuery>.Success(new(emissor, pessoa, unidade, perfil, lida, page.AfterId, page.Limit, page.Direction))
            : Recusa(ConcessaoErrorCodes.FiltroDeSituacaoInvalido, "Situação inválida. Use ativa, revogada, vencida ou todas.");
    }

    private static string? Aparado(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

    private static SituacaoDaConcessao? LerSituacao(string? texto) =>
        string.IsNullOrWhiteSpace(texto)
            ? SituacaoDaConcessao.Ativa
            : texto.Trim().ToLowerInvariant() switch
            {
                "ativa" => SituacaoDaConcessao.Ativa,
                "revogada" => SituacaoDaConcessao.Revogada,
                "vencida" => SituacaoDaConcessao.Vencida,
                "todas" => SituacaoDaConcessao.Todas,
                _ => null,
            };

    private static Result<ListarConcessoesQuery> Recusa(string codigo, string mensagem) =>
        Result<ListarConcessoesQuery>.Failure(new DomainError(codigo, mensagem));
}
