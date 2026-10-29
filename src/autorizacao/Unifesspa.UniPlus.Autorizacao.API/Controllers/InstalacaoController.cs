namespace Unifesspa.UniPlus.Autorizacao.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Instalacoes;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Queries.Instalacoes;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Infrastructure.Core.Authorization;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Instalação dos primeiros gestores de acessos sob <c>/api/autorizacao/instalacao</c>, em quatro passos de quem tem
/// o papel de instalação: iniciar, confirmar o PDF, pedir o código e confirmar com ele. A trilha e as tentativas sem
/// pendência são de quem administra concessões.
/// </summary>
/// <remarks>
/// Os passos exigem a política <c>Instalacao</c>: sem o papel a resposta é <c>403</c>, e sem token, <c>401</c>. O
/// papel não concede acesso a mais nada. A instalação é sempre a do executor do token; a de outra pessoa responde
/// <c>404</c>. A trilha e a listagem exigem <c>autorizacao:concessoes:administrar</c> pelo ponto de decisão único.
/// </remarks>
[ApiController]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public.")]
public sealed class InstalacaoController : ControllerBase
{
    private const string ResourceTag = "tentativas-sem-pendencia";
    private const string PoliticaDeInstalacao = "Instalacao";

    /// <summary>Tipo de recurso que a decisão de acesso enxerga para a trilha, comparado por igualdade exata.</summary>
    private const string RecursoTipo = "instalacao";

    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;

    public InstalacaoController(ICommandBus commandBus, IQueryBus queryBus, IDomainErrorMapper mapper)
    {
        _commandBus = commandBus;
        _queryBus = queryBus;
        _mapper = mapper;
    }

    /// <summary>
    /// Passo 1: confere as recusas de negócio, cancela a pendência anterior de quem executa e devolve a URL de PUT
    /// assinada para o PDF (validade curta, <c>Content-Type</c> <c>application/pdf</c>).
    /// </summary>
    [HttpPost("instalacao")]
    [Authorize(Policy = PoliticaDeInstalacao)]
    // TTL alinhado ao da URL de PUT devolvida: um replay depois de ela expirar devolveria uma URL inutilizável.
    [RequiresIdempotencyKey(TtlSeconds = 15 * 60)]
    [ProducesResponseType(typeof(InstalacaoIniciadaDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Iniciar([FromBody] IniciarInstalacaoRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        IdentidadeInformada[] gestores =
        [
            .. (request.Gestores ?? []).Select(g => new IdentidadeInformada(g?.Emissor, g?.Sub, g?.Email, g?.Nome)),
        ];

        Result<InstalacaoIniciadaDto> resultado = await _commandBus
            .Send(new IniciarInstalacaoCommand(gestores, request.NumeroDoDocumento, request.TamanhoDoPdf), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess
            ? CreatedAtAction(nameof(Obter), new { id = resultado.Value!.Id }, resultado.Value)
            : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Passo 2: quem iniciou informa o <c>ETag</c> que o PUT devolveu. A API não lê o objeto: grava o documento como
    /// em verificação e publica o pedido ao verificador na mesma transação.
    /// </summary>
    [HttpPost("instalacao/{id:guid}/documento/confirmacao")]
    [Authorize(Policy = PoliticaDeInstalacao)]
    [RequiresIdempotencyKey]
    [ProducesResponseType(typeof(InstalacaoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ConfirmarDocumento(
        Guid id, [FromBody] ConfirmarDocumentoDaInstalacaoRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<InstalacaoDto> resultado = await _commandBus
            .Send(new ConfirmarDocumentoDaInstalacaoCommand(id, request.Etag), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Passo 3: com o documento verificado, envia o código ao e-mail institucional verificado do token de quem
    /// chama. O código vale 2 horas, uma vez, e o endereço nunca vem do corpo da requisição. A mesma rota reenvia
    /// enquanto a instalação aguarda o código: o novo código invalida o anterior e o prazo recomeça.
    /// </summary>
    [HttpPost("instalacao/{id:guid}/codigo")]
    [Authorize(Policy = PoliticaDeInstalacao)]
    [RequiresIdempotencyKey]
    [ProducesResponseType(typeof(InstalacaoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> EnviarCodigo(Guid id, CancellationToken cancellationToken)
    {
        Result<InstalacaoDto> resultado = await _commandBus
            .Send(new EnviarCodigoDaInstalacaoCommand(id), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Passo 4: a mesma pessoa informa o código em até 2 horas, e só então os dois gestores passam a valer. Toda
    /// recusa de código tem a mesma resposta, sem revelar se há instalação pendente.
    /// </summary>
    [HttpPost("instalacao/confirmacao")]
    [Authorize(Policy = PoliticaDeInstalacao)]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Confirmar([FromBody] ConfirmarInstalacaoRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result resultado = await _commandBus
            .Send(new ConfirmarInstalacaoCommand(request.Codigo), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }

    /// <summary>Situação da instalação, só para quem a iniciou; a de outra pessoa responde 404.</summary>
    [HttpGet("instalacao/{id:guid}")]
    [Authorize(Policy = PoliticaDeInstalacao)]
    [VendorMediaType(Resource = "instalacao", Versions = [1])]
    [ProducesResponseType(typeof(InstalacaoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> Obter(Guid id, CancellationToken cancellationToken)
    {
        Result<InstalacaoDto> resultado = await _queryBus
            .Send(new ObterInstalacaoQuery(id), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Trilha da instalação: quem executou, o documento, o envio do código, as tentativas e a confirmação, sem o
    /// código. Vale também para a instalação cancelada ou expirada, que não gerou concessão.
    /// </summary>
    [HttpGet("instalacoes/{id:guid}/trilha")]
    [Authorize]
    [ExigePermissao<PermissaoAutorizacao>(PermissaoAutorizacao.ConcessoesAdministrar, RecursoTipo = RecursoTipo)]
    [VendorMediaType(Resource = "trilha-da-instalacao", Versions = [1])]
    [ProducesResponseType(typeof(IReadOnlyList<EventoDaTrilhaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> Trilha(Guid id, CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<EventoDaTrilhaDto>> resultado = await _queryBus
            .Send(new ObterTrilhaDaInstalacaoQuery(id), cancellationToken)
            .ConfigureAwait(false);

        return resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Tentativas de confirmar a instalação de quem não tinha instalação aguardando código, paginadas por cursor
    /// opaco bidirecional (ADR-0026 + ADR-0089). A trilha de uma instalação não as alcança.
    /// </summary>
    [HttpGet("instalacoes/tentativas-sem-pendencia")]
    [Authorize]
    [ExigePermissao<PermissaoAutorizacao>(PermissaoAutorizacao.ConcessoesAdministrar, RecursoTipo = RecursoTipo)]
    [VendorMediaType(Resource = "tentativa-de-instalacao", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<TentativaDeInstalacaoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    public async Task<IActionResult> ListarTentativasSemPendencia(
        [FromCursor(ResourceTag)] PageRequest page,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);

        ListarTentativasSemPendenciaResult resultado = await _queryBus.Send(
            new ListarTentativasSemPendenciaQuery(page.AfterId, page.Limit, page.Direction),
            cancellationToken).ConfigureAwait(false);

        return await this.OkPaginatedAsync(
            resultado.Items,
            resultado.AnteriorAfterId,
            resultado.ProximoAfterId,
            page,
            ResourceTag,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
